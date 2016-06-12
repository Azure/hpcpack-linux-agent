#include <cpprest/http_client.h>
#include <memory>
#include <boost/uuid/string_generator.hpp>
#include <boost/uuid/uuid_io.hpp>

#include "RemoteExecutor.h"
#include "HttpReporter.h"
#include "UdpReporter.h"
#include "../utils/WriterLock.h"
#include "../utils/ReaderLock.h"
#include "../utils/Logger.h"
#include "../utils/System.h"
#include "../common/ErrorCodes.h"
#include "../data/ProcessStatistics.h"
#include "NodeManagerConfig.h"
#include "HttpHelper.h"

using namespace web::http;
using namespace web;
using namespace hpc::core;
using namespace hpc::utils;
using namespace hpc::arguments;
using namespace hpc::data;
using namespace hpc::common;

RemoteExecutor::RemoteExecutor(const std::string& networkName)
    : monitor(System::GetNodeName(), networkName, MetricReportInterval), lock(PTHREAD_RWLOCK_INITIALIZER)
{
    this->registerReporter =
        std::unique_ptr<Reporter<json::value>>(
            new HttpReporter(
                NodeManagerConfig::GetRegisterUri(),
                3,
                this->RegisterInterval,
                [this]() { return this->monitor.GetRegisterInfo(); }));

    this->registerReporter->Start();

    this->StartHeartbeat(NodeManagerConfig::GetHeartbeatUri());
    this->StartMetric(NodeManagerConfig::GetMetricUri());
    this->StartHostsManager();
}

pplx::task<json::value> RemoteExecutor::StartJobAndTask(StartJobAndTaskArgs&& args, std::string&& callbackUri)
{
    {
        WriterLock writerLock(&this->lock);

        const auto& envi = args.StartInfo.EnvironmentVariables;
        auto isAdminIt = envi.find("CCP_ISADMIN");
        bool isAdmin = isAdminIt != envi.end() && isAdminIt->second == "1";

        auto mapAdminUserIt = envi.find("CCP_MAP_ADMIN_USER");
        bool mapAdminUser = mapAdminUserIt != envi.end() && mapAdminUserIt->second == "1";

        // If is admin, we won't create the user, default to root.
        // If username is empty, this is the old image, we use root.
        if ((mapAdminUser || !isAdmin) && !args.UserName.empty())
        {
            auto preserveDomainIt = envi.find("CCP_PRESERVE_DOMAIN");
            bool preserveDomain = preserveDomainIt != envi.end() && preserveDomainIt->second == "1";

            std::string userName = preserveDomain ? args.UserName : String::GetUserName(args.UserName);
            if (userName == "root") { userName = "hpc_faked_root"; }

            int ret = System::CreateUser(userName, args.Password);

            bool existed = ret == 9;

            if (ret != 0 && ret != 9)
            {
                throw std::runtime_error(
                    String::Join(" ", "Create user", userName, "failed with error code", ret));
            }

            std::string privateKeyFile;
            bool privateKeyAdded = 0 == System::AddSshKey(userName, args.PrivateKey, "id_rsa", "600", privateKeyFile);

            std::string publicKeyFile;
            bool publicKeyAdded = privateKeyAdded && (0 == System::AddSshKey(userName, args.PublicKey, "id_rsa.pub", "644", publicKeyFile));

            std::string userAuthKeyFile;
            bool authKeyAdded = privateKeyAdded && publicKeyAdded && (0 == System::AddAuthorizedKey(userName, args.PublicKey, "600", userAuthKeyFile));

            Logger::Debug(args.JobId, args.TaskId, this->UnknowId,
                "Create user {0} result: ret {1}, private {2}, public {3}, auth {4}",
                userName, ret, privateKeyAdded, publicKeyAdded, authKeyAdded);

            if (this->jobUsers.find(args.JobId) == this->jobUsers.end())
            {
                Logger::Debug(args.JobId, args.TaskId, this->UnknowId,
                    "Create user: jobUsers entry added.");

                this->jobUsers[args.JobId] =
                    std::tuple<std::string, bool, bool, bool, bool, std::string>(userName, existed, privateKeyAdded, publicKeyAdded, authKeyAdded, args.PublicKey);
            }

            auto it = this->userJobs.find(userName);

            if (it != this->userJobs.end())
            {
                it->second.insert(args.JobId);
            }
            else
            {
                this->userJobs[userName] = { args.JobId };
            }
        }
        else
        {
            // run as root
            Logger::Debug(args.JobId, args.TaskId, this->UnknowId,
                "Run the job as root.");

            this->jobUsers[args.JobId] =
                std::tuple<std::string, bool, bool, bool, bool, std::string>("root", true, false, false, false, "");
        }
    }

    return this->StartTask(StartTaskArgs(args.JobId, args.TaskId, std::move(args.StartInfo)), std::move(callbackUri));
}

pplx::task<json::value> RemoteExecutor::StartTask(StartTaskArgs&& args, std::string&& callbackUri)
{
    WriterLock writerLock(&this->lock);

    bool isNewEntry;
    std::shared_ptr<TaskInfo> taskInfo = this->jobTaskTable.AddJobAndTask(args.JobId, args.TaskId, isNewEntry);

    taskInfo->Affinity = args.StartInfo.Affinity;
    taskInfo->SetTaskRequeueCount(args.StartInfo.TaskRequeueCount);

    if (args.StartInfo.CommandLine.empty())
    {
        Logger::Info(args.JobId, args.TaskId, args.StartInfo.TaskRequeueCount, "MPI non-master task found, skip creating the process.");
    }
    else
    {
        if (this->processes.find(taskInfo->ProcessKey) == this->processes.end() &&
            isNewEntry)
        {
            std::string userName = "root";
            auto jobUser = this->jobUsers.find(args.JobId);
            if (jobUser == this->jobUsers.end())
            {
                throw std::runtime_error(String::Join(" ", "Job", args.JobId, "was not started on this node."));
            }
            else
            {
                userName = std::get<0>(jobUser->second);
            }

            auto process = std::shared_ptr<Process>(new Process(
                taskInfo->JobId,
                taskInfo->TaskId,
                taskInfo->GetTaskRequeueCount(),
                std::move(args.StartInfo.CommandLine),
                std::move(args.StartInfo.StdOutFile),
                std::move(args.StartInfo.StdErrFile),
                std::move(args.StartInfo.StdInFile),
                std::move(args.StartInfo.WorkDirectory),
                userName,
                std::move(args.StartInfo.Affinity),
                std::move(args.StartInfo.EnvironmentVariables),
                [taskInfo, uri = std::move(callbackUri), this] (
                    int exitCode,
                    std::string&& message,
                    const ProcessStatistics& stat)
                {
                    try
                    {
                        json::value jsonBody;

                        taskInfo->CancelGracefulThread();

                        {
                            WriterLock writerLock(&this->lock);

                            if (taskInfo->Exited)
                            {
                                Logger::Debug(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetTaskRequeueCount(),
                                    "Ended already by EndTask.");
                            }
                            else
                            {
                                taskInfo->Exited = true;
                                taskInfo->ExitCode = exitCode;
                                taskInfo->Message = std::move(message);
                                taskInfo->AssignFromStat(stat);

                                jsonBody = taskInfo->ToCompletionEventArgJson();
                            }
                        }

                        this->ReportTaskCompletion(taskInfo->JobId, taskInfo->TaskId,
                            taskInfo->GetTaskRequeueCount(), jsonBody, uri);

                        // this won't remove the task entry added later as attempt id doesn't match
                        this->jobTaskTable.RemoveTask(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetAttemptId());
                    }
                    catch (const std::exception& ex)
                    {
                        Logger::Error(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetTaskRequeueCount(),
                            "Exception when sending back task result. {0}", ex.what());
                    }

                    Logger::Debug(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetTaskRequeueCount(),
                        "attemptId {0}, processKey {1}, erasing process", taskInfo->GetAttemptId(), taskInfo->ProcessKey);

                    {
                        WriterLock writerLock(&this->lock);

                        // Process will be deleted here.
                        this->processes.erase(taskInfo->ProcessKey);
                    }
                }));

            this->processes[taskInfo->ProcessKey] = process;
            Logger::Debug(
                args.JobId, args.TaskId, taskInfo->GetTaskRequeueCount(),
                "StartTask for ProcessKey {0}, process count {1}", taskInfo->ProcessKey, this->processes.size());

            process->Start().then([this, taskInfo] (std::pair<pid_t, pthread_t> ids)
            {
                if (ids.first > 0)
                {
                    Logger::Debug(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetTaskRequeueCount(),
                        "Process started pid {0}, tid {1}", ids.first, ids.second);
                }
            });
        }
        else
        {
            Logger::Warn(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetTaskRequeueCount(),
                "The task has started already.");
        }
    }

    return pplx::task_from_result(json::value());
}

pplx::task<json::value> RemoteExecutor::EndJob(hpc::arguments::EndJobArgs&& args)
{
    WriterLock writerLock(&this->lock);

    Logger::Info(args.JobId, this->UnknowId, this->UnknowId, "EndJob: starting");

    auto jobInfo = this->jobTaskTable.RemoveJob(args.JobId);

    json::value jsonBody;

    if (jobInfo)
    {
        for (auto& taskPair : jobInfo->Tasks)
        {
            auto taskInfo = taskPair.second;

            if (taskInfo)
            {
                const auto* stat = this->TerminateTask(
                    args.JobId, taskPair.first, taskInfo->GetTaskRequeueCount(),
                    taskInfo->ProcessKey, (int)ErrorCodes::EndJobExitCode, true);
                Logger::Debug(args.JobId, taskPair.first, taskInfo->GetTaskRequeueCount(), "EndJob: Terminating task");
                if (stat != nullptr)
                {
                    taskInfo->Exited = stat->IsTerminated();
                    taskInfo->ExitCode = (int)ErrorCodes::EndJobExitCode;
                    taskInfo->AssignFromStat(*stat);
                    taskInfo->CancelGracefulThread();
                }
            }
            else
            {
                Logger::Warn(args.JobId, taskPair.first, this->UnknowId,
                    "EndJob: Task is already finished");

                assert(false);
            }
        }

        jsonBody = jobInfo->ToJson();
        Logger::Info(args.JobId, this->UnknowId, this->UnknowId, "EndJob: ended {0}", jsonBody);
    }
    else
    {
        Logger::Warn(args.JobId, this->UnknowId, this->UnknowId, "EndJob: Job is already finished");
    }

    auto jobUser = this->jobUsers.find(args.JobId);

    if (jobUser != this->jobUsers.end())
    {
        Logger::Info(args.JobId, this->UnknowId, this->UnknowId, "EndJob: Cleanup user {0}", std::get<0>(jobUser->second));
        auto userJob = this->userJobs.find(std::get<0>(jobUser->second));

        bool cleanupUser = false;
        if (userJob == this->userJobs.end())
        {
            cleanupUser = true;
        }
        else
        {
            userJob->second.erase(args.JobId);

            // cleanup when no one is using the user;
            cleanupUser = userJob->second.empty();
            Logger::Info(args.JobId, this->UnknowId, this->UnknowId,
                "EndJob: {0} jobs associated with the user {1}", userJob->second.size(), std::get<0>(jobUser->second));

            if (cleanupUser)
            {
                this->userJobs.erase(userJob);
            }
        }

        if (cleanupUser)
        {
            std::string userName, publicKey;
            bool existed, privateKeyAdded, publicKeyAdded, authKeyAdded;

            std::tie(userName, existed, privateKeyAdded, publicKeyAdded, authKeyAdded, publicKey) = jobUser->second;

            // the existed could be true for the later job, so the user will be left
            // on the node, which is by design.
            // we just have this delete user logic for a simple way of cleanup.
            // if delete user failed, cleanup keys as necessary.

            bool cleanupKeys = true;

//            if (!existed)
//            {
//                if (!userName.empty())
//                {
//                    Logger::Info(args.JobId, this->UnknowId, this->UnknowId,
//                        "EndJob: Delete user {0}", userName);
//
//                    cleanupKeys = 0 != System::DeleteUser(userName);
//                }
//            }

            if (cleanupKeys)
            {
                if (privateKeyAdded)
                {
                    Logger::Info(args.JobId, this->UnknowId, this->UnknowId,
                        "EndJob: RemoveSshKey id_rsa: {0}", userName);

                    System::RemoveSshKey(userName, "id_rsa");
                }

                if (publicKeyAdded)
                {
                    Logger::Info(args.JobId, this->UnknowId, this->UnknowId,
                        "EndJob: RemoveSshKey id_rsa.pub: {0}", userName);

                    System::RemoveSshKey(userName, "id_rsa.pub");
                }

                if (authKeyAdded)
                {
                    Logger::Info(args.JobId, this->UnknowId, this->UnknowId,
                        "EndJob: RemoveAuthorizedKey {0}", userName);

                    System::RemoveAuthorizedKey(userName, publicKey);
                }
            }
        }

        this->jobUsers.erase(jobUser);
    }

    return pplx::task_from_result(jsonBody);
}

pplx::task<json::value> RemoteExecutor::EndTask(hpc::arguments::EndTaskArgs&& args, std::string&& callbackUri)
{
    ReaderLock readerLock(&this->lock);
    Logger::Info(args.JobId, args.TaskId, this->UnknowId, "EndTask: starting");

    auto taskInfo = this->jobTaskTable.GetTask(args.JobId, args.TaskId);

    json::value jsonBody;

    if (taskInfo)
    {
        Logger::Debug(
            args.JobId, args.TaskId, taskInfo->GetTaskRequeueCount(),
            "EndTask for ProcessKey {0}, processes count {1}",
            taskInfo->ProcessKey, this->processes.size());

        const auto* stat = this->TerminateTask(
            args.JobId, args.TaskId, taskInfo->GetTaskRequeueCount(),
            taskInfo->ProcessKey,
            (int)ErrorCodes::EndTaskExitCode,
            args.TaskCancelGracePeriodSeconds == 0);

        taskInfo->ExitCode = (int)ErrorCodes::EndTaskExitCode;

        if (stat == nullptr || stat->IsTerminated())
        {
            this->jobTaskTable.RemoveTask(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetAttemptId());

            taskInfo->Exited = true;
            taskInfo->CancelGracefulThread();

            if (stat != nullptr)
            {
                taskInfo->AssignFromStat(*stat);
            }
        }
        else
        {
            taskInfo->Exited = false;
            taskInfo->AssignFromStat(*stat);

            // start a thread to kill the task after a period of time;
            auto* taskIds = new std::tuple<int, int, int, uint64_t, std::string, int, RemoteExecutor*>(
                taskInfo->JobId, taskInfo->TaskId, taskInfo->GetTaskRequeueCount(), taskInfo->ProcessKey,
                callbackUri, args.TaskCancelGracePeriodSeconds, this);

            pthread_create(&taskInfo->GracefulThreadId, nullptr, RemoteExecutor::GracePeriodElapsed, taskIds);
        }

        jsonBody = taskInfo->ToJson();
        Logger::Info(args.JobId, args.TaskId, this->UnknowId, "EndTask: ended {0}", jsonBody);
    }
    else
    {
        Logger::Warn(args.JobId, args.TaskId, this->UnknowId, "EndTask: Task is already finished");
    }

    return pplx::task_from_result(jsonBody);
}

void* RemoteExecutor::GracePeriodElapsed(void* data)
{
    pthread_setcancelstate(PTHREAD_CANCEL_ENABLE, nullptr);
    pthread_setcanceltype(PTHREAD_CANCEL_ASYNCHRONOUS, nullptr);

    auto* ids = static_cast<std::tuple<int, int, int, uint64_t, std::string, int, RemoteExecutor*>*>(data);

    int jobId, taskId, requeueCount, period;
    uint64_t processKey;
    std::string callbackUri;
    RemoteExecutor* e;
    std::tie(jobId, taskId, requeueCount, processKey, callbackUri, period, e) = *ids;

    delete ids;

    sleep(period);

    WriterLock writerLock(&e->lock);

    Logger::Info(jobId, taskId, e->UnknowId, "GracePeriodElapsed: starting");

    auto taskInfo = e->jobTaskTable.GetTask(jobId, taskId);

    if (taskInfo)
    {
        const auto* stat = e->TerminateTask(
            jobId, taskId, requeueCount,
            processKey,
            (int)ErrorCodes::EndTaskExitCode,
            true);

        if (stat != nullptr)
        {
            // stat == nullptr means the processKey is already removed from the map
            // which means the main task has exited already.
            taskInfo->Exited = true;
            taskInfo->ExitCode = (int)ErrorCodes::EndTaskExitCode;
            e->jobTaskTable.RemoveTask(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetAttemptId());

            taskInfo->AssignFromStat(*stat);
            json::value jsonBody = taskInfo->ToCompletionEventArgJson();
            Logger::Info(jobId, taskId, e->UnknowId, "EndTask: ended {0}", jsonBody);
            e->ReportTaskCompletion(jobId, taskId, requeueCount, jsonBody, callbackUri);
        }
    }
    else
    {
        Logger::Warn(jobId, taskId, e->UnknowId, "EndTask: Task is already finished");
    }

    pthread_exit(nullptr);
}

void RemoteExecutor::ReportTaskCompletion(
    int jobId, int taskId, int taskRequeueCount, json::value jsonBody,
    const std::string& callbackUri)
{
    try
    {
        if (!jsonBody.is_null())
        {
            Logger::Debug(jobId, taskId, taskRequeueCount,
                "Callback to {0} with {1}", callbackUri, jsonBody);

            client::http_client client = HttpHelper::GetHttpClient(callbackUri);
            http_request request = HttpHelper::GetHttpRequest(methods::POST, jsonBody);
            http_response response = client.request(request).get();

            Logger::Info(jobId, taskId, taskRequeueCount,
                "Callback to {0} response code {1}", callbackUri, response.status_code());
        }
    }
    catch (const std::exception& ex)
    {
        this->jobTaskTable.RequestResync();
        Logger::Error(jobId, taskId, taskRequeueCount,
            "Exception when sending back task result. {0}", ex.what());
    }
}

void RemoteExecutor::StartHeartbeat(std::string&& callbackUri)
{
    WriterLock writerLock(&this->lock);

    this->nodeInfoReporter =
        std::unique_ptr<Reporter<json::value>>(
            new HttpReporter(
                callbackUri,
                0,
                this->NodeInfoReportInterval,
                [this]() { return this->jobTaskTable.ToJson(); }));

    this->nodeInfoReporter->Start();
}

void RemoteExecutor::StartHostsManager()
{
    std::string hostsUri = NodeManagerConfig::GetHostsFileUri();
    if (!hostsUri.empty())
    {
        int interval = this->DefaultHostsFetchInterval;

        try
        {
            interval = NodeManagerConfig::GetHostsFetchInterval();
        }
        catch (...)
        {
            // The Hosts Fetch interval may be not specified, just use the default interval in this case.
            Logger::Info("HostsFetchInterval not specified or invalid, use the default interval {0} seconds.", interval);
        }

        if (interval < MinHostsFetchInterval)
        {
            Logger::Info("HostsFetchInterval {0} is less than minimum interval {1}, use the minimum interval.", interval, MinHostsFetchInterval);
            interval = MinHostsFetchInterval;
        }

        WriterLock writerLock(&this->lock);

        this->hostsManager = std::unique_ptr<HostsManager>(new HostsManager(hostsUri, interval));
        this->hostsManager->Start();
    }
    else
    {
        Logger::Warn("HostsFileUri not specified, hosts manager will not be started.");
    }
}

pplx::task<json::value> RemoteExecutor::Ping(std::string&& callbackUri)
{
    auto uri = NodeManagerConfig::GetHeartbeatUri();

    if (uri != callbackUri)
    {
        NodeManagerConfig::SaveHeartbeatUri(callbackUri);
        this->StartHeartbeat(std::move(callbackUri));
    }

    return pplx::task_from_result(json::value());
}

void RemoteExecutor::StartMetric(std::string&& callbackUri)
{
    WriterLock writerLock(&this->lock);

    if (!callbackUri.empty())
    {
        auto tokens = String::Split(callbackUri, '/');
        uuid id = string_generator()(tokens[4]);

        this->monitor.SetNodeUuid(id);

        this->metricReporter =
            std::unique_ptr<Reporter<std::vector<unsigned char>>>(
                new UdpReporter(
                    callbackUri,
                    0,
                    this->MetricReportInterval,
                    [this]() { return this->monitor.GetMonitorPacketData(); }));

        this->metricReporter->Start();
    }
}

pplx::task<json::value> RemoteExecutor::Metric(std::string&& callbackUri)
{
    auto uri = NodeManagerConfig::GetMetricUri();
    if (uri != callbackUri)
    {
        NodeManagerConfig::SaveMetricUri(callbackUri);

        // callbackUri is like udp://server:port/api/nodeguid/metricreported
        this->StartMetric(std::move(callbackUri));
    }

    return pplx::task_from_result(json::value());
}

pplx::task<json::value> RemoteExecutor::MetricConfig(
    MetricCountersConfig&& config,
    std::string&& callbackUri)
{
    this->Metric(std::move(callbackUri));

    this->monitor.ApplyMetricConfig(std::move(config));

    return pplx::task_from_result(json::value());
}

const ProcessStatistics* RemoteExecutor::TerminateTask(
    int jobId, int taskId, int requeueCount,
    uint64_t processKey, int exitCode, bool forced)
{
    auto p = this->processes.find(processKey);
//    Logger::Debug(
//        jobId, taskId, requeueCount,
//        "TerminateTask for ProcessKey {0}, processes count {1}",
//        processKey, this->processes.size());
//
//    for (auto pro : this->processes)
//    {
//        Logger::Debug(
//            jobId, taskId, requeueCount,
//            "TerminateTask process list {0}",
//            pro.first);
//    }

    if (p != this->processes.end())
    {
        Logger::Debug(jobId, taskId, requeueCount, "About to Kill the task, forced {0}.", forced);
        p->second->Kill(exitCode, forced);

        const auto* stat = &p->second->GetStatisticsFromCGroup();

        int times = 10;
        while (!stat->IsTerminated() && times-- > 0)
        {
            sleep(0.1);
            stat = &p->second->GetStatisticsFromCGroup();
        }

        if (!stat->IsTerminated())
        {
            Logger::Warn(jobId, taskId, requeueCount,
                "The task didn't exit within 1s, process Ids {0}",
                String::Join<' '>(stat->ProcessIds));
        }

        return stat;
    }
    else
    {
        Logger::Warn(jobId, taskId, requeueCount, "No process object found.");
        return nullptr;
    }
}
