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
                this->LoadReportUri(this->RegisterUriFileName),
                3,
                this->RegisterInterval,
                [this]() { return this->monitor.GetRegisterInfo(); }));

    this->Ping(this->LoadReportUri(this->NodeInfoUriFileName));
    this->Metric(this->LoadReportUri(this->MetricUriFileName));
}

json::value RemoteExecutor::StartJobAndTask(StartJobAndTaskArgs&& args, const std::string& callbackUri)
{
    {
        WriterLock writerLock(&this->lock);

        const auto& envi = args.StartInfo.EnvironmentVariables;
        auto isAdminIt = envi.find("CCP_ISADMIN");
        bool isAdmin = isAdminIt != envi.end() && isAdminIt->second == "1";

        // If is admin, we won't create the user, default to root.
        // If username is empty, this is the old image, we use root.
        if (!isAdmin && !args.UserName.empty())
        {
            std::string userName = String::GetUserName(args.UserName);

            int ret = System::CreateUser(userName, args.Password);

            bool existed = ret == 9;

            if (ret != 0 && ret != 9)
            {
                throw std::runtime_error(
                    String::Join(" ", "Create user", userName, "failed with error code", ret));
            }

            bool privateKeyAdded = 0 == System::AddSshKey(userName, args.PrivateKey, "id_rsa");
            bool publicKeyAdded = 0 == System::AddSshKey(userName, args.PublicKey, "id_rsa.pub");
            bool authKeyAdded = 0 == System::AddAuthorizedKey(userName, args.PublicKey);

            if (authKeyAdded)
            {
                std::string output;
                std::string userAuthKeyFile = String::Join("", "/home/", userName, "/.ssh/authorized_keys");
                System::ExecuteCommandOut(output, "chmod 600", userAuthKeyFile,
                    "&& chown", userName, userAuthKeyFile);
            }

            if (privateKeyAdded)
            {
                std::string output;
                std::string privateKeyFile = String::Join("", "/home/", userName, "/.ssh/id_rsa");
                System::ExecuteCommandOut(output, "chmod 600", privateKeyFile,
                    "&& chown", userName, privateKeyFile);
            }

            if (publicKeyAdded)
            {
                std::string output;
                std::string publicKeyFile = String::Join("", "/home/", userName, "/.ssh/id_rsa.pub");
                System::ExecuteCommandOut(output, "chmod 644", publicKeyFile,
                    "&& chown", userName, publicKeyFile);
            }

            this->jobUsers[args.JobId] =
                std::tuple<std::string, bool, bool, bool, bool, std::string>(userName, existed, privateKeyAdded, publicKeyAdded, authKeyAdded, args.PublicKey);

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
    }

    return this->StartTask(StartTaskArgs(args.JobId, args.TaskId, std::move(args.StartInfo)), callbackUri);
}

json::value RemoteExecutor::StartTask(StartTaskArgs&& args, const std::string& callbackUri)
{
    WriterLock writerLock(&this->lock);

    bool isNewEntry;
    std::shared_ptr<TaskInfo> taskInfo = this->jobTaskTable.AddJobAndTask(args.JobId, args.TaskId, isNewEntry);

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
                Logger::Error(args.JobId, args.TaskId, args.StartInfo.TaskRequeueCount,
                    "no user created, run as root");
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
                [taskInfo, callbackUri, this] (
                    int exitCode,
                    std::string&& message,
                    uint64_t userTimeMs,
                    uint64_t kernelTimeMs,
                    std::vector<int>&& processIds,
                    uint64_t workingSetKb)
                {
                    try
                    {
                        json::value jsonBody;

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
                                taskInfo->KernelProcessorTimeMs = kernelTimeMs;
                                taskInfo->UserProcessorTimeMs = userTimeMs;
                                taskInfo->ProcessIds = std::move(processIds);
                                taskInfo->WorkingSetKb = workingSetKb;

                                jsonBody = taskInfo->ToCompletionEventArgJson();
                            }
                        }

                        if (!jsonBody.is_null())
                        {
                            Logger::Debug(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetTaskRequeueCount(),
                                "Callback to {0} with {1}", callbackUri, jsonBody);
                            client::http_client_config config;
                            config.set_validate_certificates(false);
                            utility::seconds timeout(5l);
                            config.set_timeout(timeout);

                            Logger::Debug(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetTaskRequeueCount(),
                                "Callback to {0}, configure: timeout {1} seconds, chuck size {2}",
                                callbackUri, config.timeout().count(), config.chunksize());

                            client::http_client client(callbackUri, config);
                            http_response response = client.request(methods::POST, "", jsonBody).get();

                            Logger::Info(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetTaskRequeueCount(),
                                "Callback to {0} response code {1}", callbackUri, response.status_code());
                        }

                        // this won't remove the task entry added later as attempt id doesn't match
                        this->jobTaskTable.RemoveTask(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetAttemptId());
                    }
                    catch (const std::exception& ex)
                    {
                        this->jobTaskTable.RequestResync();
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

            process->Start().then([this, taskInfo] (pid_t pid)
            {
                if (pid > 0)
                {
                    Logger::Debug(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetTaskRequeueCount(),
                        "Process started {0}", pid);
                }
            });
        }
        else
        {
            Logger::Warn(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetTaskRequeueCount(),
                "The task has started already.");
        }
    }

    return json::value();
}

json::value RemoteExecutor::EndJob(hpc::arguments::EndJobArgs&& args)
{
    WriterLock writerLock(&this->lock);

    Logger::Info(args.JobId, this->UnknowId, this->UnknowId, "EndJob: starting");

    auto jobInfo = this->jobTaskTable.RemoveJob(args.JobId);

    json::value jsonBody;

    if (jobInfo)
    {
        for (auto& taskPair : jobInfo->Tasks)
        {
            this->TerminateTask(taskPair.second->ProcessKey, (int)ErrorCodes::EndJobExitCode);

            auto taskInfo = taskPair.second;

            if (taskInfo)
            {
                taskInfo->Exited = true;
                taskInfo->ExitCode = (int)ErrorCodes::EndJobExitCode;
                Logger::Debug(args.JobId, taskPair.first, taskInfo->GetTaskRequeueCount(), "EndJob: starting");
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
            if (!existed)
            {
                if (!userName.empty())
                {
                    Logger::Info(args.JobId, this->UnknowId, this->UnknowId,
                        "EndJob: Delete user {0}", userName);
                    System::DeleteUser(userName);
                }
            }
            else
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

    return jsonBody;
}

json::value RemoteExecutor::EndTask(hpc::arguments::EndTaskArgs&& args)
{
    ReaderLock readerLock(&this->lock);
    Logger::Info(args.JobId, args.TaskId, this->UnknowId, "EndTask: starting");

    auto taskInfo = this->jobTaskTable.GetTask(args.JobId, args.TaskId);

    json::value jsonBody;

    if (taskInfo)
    {
        this->TerminateTask(taskInfo->ProcessKey, (int)ErrorCodes::EndTaskExitCode);
        this->jobTaskTable.RemoveTask(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetAttemptId());

        taskInfo->Exited = true;
        taskInfo->ExitCode = (int)ErrorCodes::EndTaskExitCode;

        jsonBody = taskInfo->ToJson();
        Logger::Info(args.JobId, args.TaskId, this->UnknowId, "EndTask: ended {0}", jsonBody);
    }
    else
    {
        Logger::Warn(args.JobId, args.TaskId, this->UnknowId, "EndTask: Task is already finished");
    }

    return jsonBody;
}

json::value RemoteExecutor::Ping(const std::string& callbackUri)
{
    this->SaveReportUri(this->NodeInfoUriFileName, callbackUri);
    this->nodeInfoReporter =
        std::unique_ptr<Reporter<json::value>>(
            new HttpReporter(
                callbackUri,
                0,
                this->NodeInfoReportInterval,
                [this]() { return this->jobTaskTable.ToJson(); }));

    return json::value();
}

json::value RemoteExecutor::Metric(const std::string& callbackUri)
{
    this->SaveReportUri(this->MetricUriFileName, callbackUri);

    // callbackUri is like udp://server:port/api/nodeguid/metricreported
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
    }

    return json::value();
}

bool RemoteExecutor::TerminateTask(int processKey, int exitCode)
{
    auto p = this->processes.find(processKey);

    if (p != this->processes.end())
    {
        p->second->Kill(exitCode);

        return true;
    }
    else
    {
        return false;
    }
}

std::string RemoteExecutor::LoadReportUri(const std::string& fileName)
{
    std::ifstream fs(fileName, std::ios::in);
    std::string uri;

    if (fs.good())
    {
        fs >> uri;
    }

    fs.close();

    return std::move(uri);
}

void RemoteExecutor::SaveReportUri(const std::string& fileName, const std::string& uri)
{
    std::ofstream fs(fileName, std::ios::trunc);
    fs << uri;
    fs.close();
}
