#include "RemoteExecutor.h"
#include "../utils/WriterLock.h"
#include "../utils/Logger.h"

#include <cpprest/http_client.h>

using namespace web::http;

using namespace hpc::core;
using namespace hpc::utils;
using namespace hpc::arguments;
using namespace hpc::data;

RemoteExecutor::RemoteExecutor() : lock(PTHREAD_RWLOCK_INITIALIZER)
{
    //ctor
}

bool RemoteExecutor::StartJobAndTask(StartJobAndTaskArgs&& args, const std::string& callbackUri)
{
    // TODO: Prepare Job execution envi.

    return this->StartTask(StartTaskArgs(args.JobId, args.TaskId, std::move(args.StartInfo)), callbackUri);
}

bool RemoteExecutor::StartTask(StartTaskArgs&& args, const std::string& callbackUri)
{
    this->jobTaskTable.AddJobAndTask(args.JobId, args.TaskId, TaskInfo(args.JobId, args.TaskId, args.StartInfo.TaskRequeueCount));

    std::map<int, Process*>::iterator p;

    {
        WriterLock(&this->lock);
        p = this->processes.find(args.TaskId);
        if (p == this->processes.end())
        {
            int jobId = args.JobId;
            int taskId = args.TaskId;

            auto process = new Process(
                std::move(args.StartInfo.CommandLine),
                std::move(args.StartInfo.WorkDirectory),
                std::move(args.StartInfo.EnvironmentVariables),
                [jobId, taskId, callbackUri, this] (int exitCode, std::string&& message, timeval userTime, timeval kernelTime)
                {
                    auto taskInfo = this->jobTaskTable.RemoveTask(jobId, taskId);
                    taskInfo.ExitCode = exitCode;
                    taskInfo.Message = std::move(message);
                    taskInfo.KernelProcessorTime = kernelTime.tv_sec * 1000000 + kernelTime.tv_usec;
                    taskInfo.UserProcessorTime = userTime.tv_sec * 1000000 + userTime.tv_usec;

                    client::http_client client(callbackUri);
                    http_request request;
                    request.set_method(methods::POST);
                    auto result = taskInfo.GetJson();
                    //std::string jsonBody = callbackBody.serialize();

                    Logger::Info("Callback to {0} with {1}", callbackUri, result);

                    request.set_body(result);
                    client.request(request).get(); // This get doesn't affect the threadpool since it will be run in the dedicated thread in the Process.

                    {
                        WriterLock(&this->lock);

                        // Remove it here, since, the process will be deleted by itself after the callback.
                        this->processes.erase(taskId);
                    }
                });

            auto insertResult = this->processes.insert(std::make_pair(args.TaskId, process));
            assert(insertResult.second);

            insertResult.first->second->Start().then([this] (pid_t pid)
            {
                if (pid > 0)
                {
                    Logger::Info("Process started {0}", pid);
                }
            });
        }
        else
        {
            // Found the original process.
            // TODO: assert the job task table call is the same.
        }
    }

    return true;
}

bool RemoteExecutor::EndJob(hpc::arguments::EndJobArgs&& args)
{
    return true;
}

bool RemoteExecutor::EndTask(hpc::arguments::EndTaskArgs&& args)
{
    return true;
}

bool RemoteExecutor::Ping(const std::string& callbackUri)
{
    return true;
}

bool RemoteExecutor::Metric(const std::string& callbackUri)
{
    return true;
}
