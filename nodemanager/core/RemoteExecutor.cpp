#include <cpprest/http_client.h>
#include <memory>

#include "RemoteExecutor.h"
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
    this->Ping(this->LoadReportUri(this->NodeInfoUriFileName));
    this->Metric(this->LoadReportUri(this->MetricUriFileName));
}

json::value RemoteExecutor::StartJobAndTask(StartJobAndTaskArgs&& args, const std::string& callbackUri)
{
    {
        WriterLock writerLock(&this->lock);

        std::string userName;
        bool existed = false;
        if (!args.UserName.empty())
        {
            userName = String::GetUserName(args.UserName);
            int ret = System::CreateUser(userName, args.Password, args.PrivateKey, args.PublicKey);
            if (ret == 9)
            {
                existed = true;
            }
            else if (ret != 0)
            {
                throw std::runtime_error(
                    String::Join(" ", "Create user", userName, "failed with error code", ret));
            }
        }

        this->jobUsers[args.JobId] = std::tuple<std::string, std::string, bool>(userName, args.Password, existed);
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

            auto jobUser = this->jobUsers.find(args.JobId);
            if (jobUser == this->jobUsers.end())
            {
                Logger::Error(args.JobId, args.TaskId, args.StartInfo.TaskRequeueCount,
                    "no user created");
                throw new std::runtime_error("no user created");
            }

            auto process = std::shared_ptr<Process>(new Process(
                taskInfo->JobId,
                taskInfo->TaskId,
                taskInfo->GetTaskRequeueCount(),
                std::move(args.StartInfo.CommandLine),
                std::move(args.StartInfo.StdOutText),
                std::move(args.StartInfo.StdErrText),
                std::move(args.StartInfo.StdInText),
                std::move(args.StartInfo.WorkDirectory),
                std::get<0>(jobUser->second),
                std::get<1>(jobUser->second),
                std::move(args.StartInfo.Affinity),
                std::move(args.StartInfo.EnvironmentVariables),
                [taskInfo, callbackUri, this] (int exitCode, std::string&& message, timeval userTime, timeval kernelTime)
                {
                    WriterLock writerLock(&this->lock);

                    try
                    {
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
                            taskInfo->KernelProcessorTime = kernelTime.tv_sec * 1000000 + kernelTime.tv_usec;
                            taskInfo->UserProcessorTime = userTime.tv_sec * 1000000 + userTime.tv_usec;

                            auto jsonBody = taskInfo->ToCompletionEventArgJson();

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
                            client.request(methods::POST, "", jsonBody).then([&callbackUri, this, taskInfo](http_response response)
                            {
                                Logger::Info(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetTaskRequeueCount(),
                                    "Callback to {0} response code {1}", callbackUri, response.status_code());
                            }).wait();
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

                    // Process will be deleted here.
                    this->processes.erase(taskInfo->ProcessKey);
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
            // Found the original process.
            // TODO: assert the job task table call is the same.
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

// Won't delete the user to avoid multi-job interfere
//    auto jobUser = this->jobUsers.find(args.JobId);
//    if (jobUser != this->jobUsers.end())
//    {
//        bool existed = std::get<2>(jobUser->second);
//
//        if (!existed)
//        {
//            auto userName = std::get<0>(jobUser->second);
//            if (!userName.empty())
//            {
//                System::DeleteUser(userName);
//            }
//        }
//    }

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
    this->nodeInfoReporter = std::unique_ptr<Reporter>(new Reporter(callbackUri, this->NodeInfoReportInterval, [this]() { return this->jobTaskTable.ToJson(); }));

    return json::value();
}

json::value RemoteExecutor::Metric(const std::string& callbackUri)
{
    this->SaveReportUri(this->MetricUriFileName, callbackUri);
    this->metricReporter = std::unique_ptr<Reporter>(new Reporter(callbackUri, this->MetricReportInterval, [this]() { return this->monitor.ToJson(); }));

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
    fs >> uri;
    fs.close();

    return std::move(uri);
}

void RemoteExecutor::SaveReportUri(const std::string& fileName, const std::string& uri)
{
    std::ofstream fs(fileName, std::ios::trunc);
    fs << uri;
    fs.close();
}
