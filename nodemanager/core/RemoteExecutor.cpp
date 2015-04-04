#include <cpprest/http_client.h>
#include <memory>

#include "RemoteExecutor.h"
#include "../utils/WriterLock.h"
#include "../utils/ReaderLock.h"
#include "../utils/Logger.h"
#include "../utils/System.h"

using namespace web::http;
using namespace web;
using namespace hpc::core;
using namespace hpc::utils;
using namespace hpc::arguments;
using namespace hpc::data;

RemoteExecutor::RemoteExecutor(const std::string& networkName)
    : monitor(System::GetNodeName(), networkName, MetricReportInterval), lock(PTHREAD_RWLOCK_INITIALIZER)
{
    this->Ping(this->LoadReportUri(this->NodeInfoUriFileName));
    this->Metric(this->LoadReportUri(this->MetricUriFileName));
}

json::value RemoteExecutor::StartJobAndTask(StartJobAndTaskArgs&& args, const std::string& callbackUri)
{
    // TODO: Prepare Job execution envi.

    return this->StartTask(StartTaskArgs(args.JobId, args.TaskId, std::move(args.StartInfo)), callbackUri);
}

json::value RemoteExecutor::StartTask(StartTaskArgs&& args, const std::string& callbackUri)
{
    WriterLock writerLock(&this->lock);

    bool isNewEntry;
    std::shared_ptr<TaskInfo> taskInfo = this->jobTaskTable.AddJobAndTask(args.JobId, args.TaskId, isNewEntry);

    if (taskInfo->TaskRequeueCount < args.StartInfo.TaskRequeueCount)
    {
        Logger::Info("Task {0}: Change requeue count from {1} to {2}", args.TaskId, taskInfo->TaskRequeueCount, args.StartInfo.TaskRequeueCount);
        taskInfo->TaskRequeueCount = args.StartInfo.TaskRequeueCount;
    }

    if (args.StartInfo.CommandLine.empty())
    {
        Logger::Info("Job {0}, task {1} MPI non-master task found, skip creating the process.", args.JobId, args.TaskId);
    }
    else
    {
        if (this->processes.find(taskInfo->GetAttemptId()) == this->processes.end() &&
            isNewEntry)
        {
            auto process = std::shared_ptr<Process>(new Process(
                args.TaskId,
                std::move(args.StartInfo.CommandLine),
                std::move(args.StartInfo.StdOutText),
                std::move(args.StartInfo.StdErrText),
                std::move(args.StartInfo.StdInText),
                std::move(args.StartInfo.WorkDirectory),
                std::move(args.StartInfo.Affinity),
                std::move(args.StartInfo.EnvironmentVariables),
                [taskInfo, callbackUri, this] (int exitCode, std::string&& message, timeval userTime, timeval kernelTime)
                {
                    WriterLock writerLock(&this->lock);

                    try
                    {
                        if (taskInfo->Exited)
                        {
                            Logger::Debug("Task {0}: Ended already by EndTask.", taskInfo->TaskId);
                        }
                        else
                        {
                            taskInfo->Exited = true;
                            taskInfo->ExitCode = exitCode;
                            taskInfo->Message = std::move(message);
                            taskInfo->KernelProcessorTime = kernelTime.tv_sec * 1000000 + kernelTime.tv_usec;
                            taskInfo->UserProcessorTime = userTime.tv_sec * 1000000 + userTime.tv_usec;

                            auto jsonBody = taskInfo->ToJson();
                            Logger::Debug("Task {0}: Callback to {1} with {2}", taskInfo->TaskId, callbackUri, jsonBody);
                            client::http_client_config config;
                            config.set_validate_certificates(false);
                            client::http_client client(callbackUri, config);
                            client.request(methods::POST, "", jsonBody).then([&callbackUri, this, taskInfo](http_response response)
                            {
                                Logger::Info("Task {0}: Callback to {1} response code {2}", taskInfo->TaskId, callbackUri, response.status_code());
                            }).wait();
                        }
                    }
                    catch (const std::exception& ex)
                    {
                        Logger::Error("Exception when sending back task result. {0}", ex.what());
                    }

                    // this won't remove the task entry added later as attempt id doesn't match
                    this->jobTaskTable.RemoveTask(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetAttemptId());

                    Logger::Debug("Task {0}: attemptId {1}, erasing process", taskInfo->TaskId, taskInfo->GetAttemptId());

                    // Process will be deleted here.
                    this->processes.erase(taskInfo->GetAttemptId());
                }));

            this->processes[taskInfo->GetAttemptId()] = process;

            process->Start().then([this] (pid_t pid)
            {
                if (pid > 0)
                {
                    Logger::Debug("Process started {0}", pid);
                }
            });
        }
        else
        {
            Logger::Warn("The task {0} has started already.", args.TaskId);
            // Found the original process.
            // TODO: assert the job task table call is the same.
        }
    }

    return json::value();
}

json::value RemoteExecutor::EndJob(hpc::arguments::EndJobArgs&& args)
{
    ReaderLock readerLock(&this->lock);

    auto jobInfo = this->jobTaskTable.RemoveJob(args.JobId);

    json::value jsonBody;
    if (jobInfo)
    {
        for (auto& taskPair : jobInfo->Tasks)
        {
            this->TerminateTask(taskPair.first);
        }

        jsonBody = jobInfo->ToJson();
    }

    return jsonBody;
}

json::value RemoteExecutor::EndTask(hpc::arguments::EndTaskArgs&& args)
{
    ReaderLock readerLock(&this->lock);

    auto taskInfo = this->jobTaskTable.GetTask(args.JobId, args.TaskId);

    this->TerminateTask(args.TaskId);

    json::value jsonBody;

    if (taskInfo)
    {
        this->jobTaskTable.RemoveTask(taskInfo->JobId, taskInfo->TaskId, taskInfo->GetAttemptId());

        taskInfo->Exited = true;
        taskInfo->ExitCode = -1;

        jsonBody = taskInfo->ToJson();
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

bool RemoteExecutor::TerminateTask(int taskId)
{
    auto p = this->processes.find(taskId);

    if (p != this->processes.end())
    {
        p->second->Kill(-1);

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
