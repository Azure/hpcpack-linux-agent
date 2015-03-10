#include <cpprest/http_client.h>
#include <memory>

#include "RemoteExecutor.h"
#include "../utils/WriterLock.h"
#include "../utils/ReaderLock.h"
#include "../utils/Logger.h"
#include "../utils/System.h"

using namespace web::http;

using namespace hpc::core;
using namespace hpc::utils;
using namespace hpc::arguments;
using namespace hpc::data;

RemoteExecutor::RemoteExecutor()
    : monitor(System::GetNodeName(), MetricReportInterval), lock(PTHREAD_RWLOCK_INITIALIZER)
{
    this->Ping(this->LoadReportUri(this->NodeInfoUriFileName));
    this->Metric(this->LoadReportUri(this->MetricUriFileName));
}

bool RemoteExecutor::StartJobAndTask(StartJobAndTaskArgs&& args, const std::string& callbackUri)
{
    // TODO: Prepare Job execution envi.

    return this->StartTask(StartTaskArgs(args.JobId, args.TaskId, std::move(args.StartInfo)), callbackUri);
}

bool RemoteExecutor::StartTask(StartTaskArgs&& args, const std::string& callbackUri)
{
    std::shared_ptr<TaskInfo> taskInfo = this->jobTaskTable.AddJobAndTask(args.JobId, args.TaskId);

    if (taskInfo->TaskRequeueCount < args.StartInfo.TaskRequeueCount)
    {
        Logger::Info("Change the task {0} requeue count from {1} to {2}", args.TaskId, taskInfo->TaskRequeueCount, args.StartInfo.TaskRequeueCount);
        taskInfo->TaskRequeueCount = args.StartInfo.TaskRequeueCount;
    }

    {
        WriterLock writerLock(&this->lock);

        if (this->processes.find(args.TaskId) == this->processes.end())
        {
            auto process = std::shared_ptr<Process>(new Process(
                args.TaskId,
                std::move(args.StartInfo.CommandLine),
                std::move(args.StartInfo.WorkDirectory),
                std::move(args.StartInfo.EnvironmentVariables),
                [taskInfo, callbackUri, this] (int exitCode, std::string&& message, timeval userTime, timeval kernelTime)
                {
                    try
                    {
                        this->jobTaskTable.RemoveTask(taskInfo->JobId, taskInfo->TaskId);
                        taskInfo->Exited = true;
                        taskInfo->ExitCode = exitCode;
                        taskInfo->Message = std::move(message);
                        taskInfo->KernelProcessorTime = kernelTime.tv_sec * 1000000 + kernelTime.tv_usec;
                        taskInfo->UserProcessorTime = userTime.tv_sec * 1000000 + userTime.tv_usec;

                        auto jsonBody = taskInfo->ToJson();
                        Logger::Debug("Callback to {0} with {1}", callbackUri, jsonBody);
                        client::http_client_config config;
                        config.set_validate_certificates(false);
                        client::http_client client(callbackUri, config);
                        client.request(methods::POST, "", jsonBody).then([&callbackUri](http_response response)
                        {
                            Logger::Info("Callback to {0} response code {1}", callbackUri, response.status_code());
                        }).wait();
                    }
                    catch (const std::exception& ex)
                    {
                        Logger::Error("Exception when sending back task result. {0}", ex.what());
                    }

                    {
                        WriterLock writerLock(&this->lock);

                        // Process will be deleted here.
                        this->processes.erase(taskInfo->TaskId);
                        Logger::Debug("erased process");
                    }
                }));

            this->processes[args.TaskId] = process;

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

    return true;
}

bool RemoteExecutor::EndJob(hpc::arguments::EndJobArgs&& args)
{
    auto jobInfo = this->jobTaskTable.RemoveJob(args.JobId);

    if (jobInfo)
    {
        for (auto& taskPair : jobInfo->Tasks)
        {
            this->TerminateTask(taskPair.first);
        }
    }

    return true;
}

bool RemoteExecutor::EndTask(hpc::arguments::EndTaskArgs&& args)
{
    this->TerminateTask(args.TaskId);
    return true;
}

bool RemoteExecutor::Ping(const std::string& callbackUri)
{
    this->SaveReportUri(this->NodeInfoUriFileName, callbackUri);
    this->nodeInfoReporter = std::unique_ptr<Reporter>(new Reporter(callbackUri, this->NodeInfoReportInterval, [this]() { return this->jobTaskTable.ToJson(); }));

    return true;
}

bool RemoteExecutor::Metric(const std::string& callbackUri)
{
    this->SaveReportUri(this->MetricUriFileName, callbackUri);
    this->metricReporter = std::unique_ptr<Reporter>(new Reporter(callbackUri, this->MetricReportInterval, [this]() { return this->monitor.ToJson(); }));

    return true;
}

bool RemoteExecutor::TerminateTask(int taskId)
{
    ReaderLock readerLock(&this->lock);
    auto p = this->processes.find(taskId);

    if (p != this->processes.end())
    {
        p->second->Kill();

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
