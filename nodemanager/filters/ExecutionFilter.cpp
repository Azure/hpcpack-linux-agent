#include "ExecutionFilter.h"
#include "../utils/Logger.h"
#include "../common/ErrorCodes.h"
#include "../utils/System.h"
#include "../core/Process.h"
#include "../data/ProcessStatistics.h"

using namespace hpc::filters;
using namespace hpc::utils;
using namespace hpc::common;
using namespace hpc::data;

int ExecutionFilter::OnJobStart(int jobId, const json::value& input, json::value& output, std::string& executionMessage)
{
    return this->ExecuteFilter(JobStartFilter, jobId, 0, 0, input, output, executionMessage);
}

int ExecutionFilter::OnJobEnd(int jobId, const json::value& input, json::value& output, std::string& executionMessage)
{
    return this->ExecuteFilter(JobEndFilter, jobId, 0, 0, input, output, executionMessage);
}

int ExecutionFilter::OnTaskStart(int jobId, int taskId, int requeueCount, const json::value& input, json::value& output, std::string& executionMessage)
{
    return this->ExecuteFilter(TaskStartFilter, jobId, taskId, requeueCount, input, output, executionMessage);
}

int ExecutionFilter::ExecuteFilter(const std::string& filterType, int jobId, int taskId, int requeueCount, const json::value& input, json::value& output, std::string& executionMessage)
{
    auto filterIt = this->filterFiles.find(filterType);
    if (filterIt == this->filterFiles.end())
    {
        Logger::Error(jobId, taskId, requeueCount, "Unknown filter type {0}", filterType);
        return (int)ErrorCodes::UnknownFilter;
    }

    std::string filterFile = filterIt->second;

    if (filterFile.empty())
    {
        Logger::Error(jobId, taskId, requeueCount, "{0} not detected, skip", filterType);
        output = input;
        return 0;
    }

    char folder[256];
    sprintf(folder, "/tmp/nodemanager_executionfilter_%d.XXXXXX", jobId);
    int ret = System::CreateTempFolder(folder, "root");

    if (ret != 0)
    {
        Logger::Error(jobId, taskId, requeueCount, "{0} {1}: Failed to create folder {2}, exit code {3}", filterType, filterFile, folder, ret);
        return ret;
    }

    std::string folderString = folder;

    std::string stdinFile = folderString + "/stdin.txt";
    ret = System::WriteStringToFile(stdinFile, input.serialize());

    if (ret != 0)
    {
        Logger::Error(jobId, taskId, requeueCount, "{0} {1}: Failed to create stdin file {2}, exit code {3}", filterType, filterFile, stdinFile, ret);
        return ret;
    }

    std::string stdoutFile = folderString + "/stdout.txt";
    std::string stderrFile = stdoutFile;

    Process p(
        jobId, taskId, requeueCount, filterFile, stdoutFile, stderrFile, stdinFile, folderString, "root",
        std::vector<uint64_t>(), std::map<std::string, std::string>(),
        [&ret, &executionMessage, jobId, taskId, requeueCount, &filterType, &filterFile]
        (int exitCode, std::string&& message, const ProcessStatistics& stat)
        {
            ret = exitCode;
            if (ret != 0)
            {
                Logger::Error(jobId, taskId, requeueCount, "{0} {1}: returned {2}, message {3}", filterType, filterFile, ret, message);
            }
            else
            {
                Logger::Info(jobId, taskId, requeueCount, "{0} {1}: success with message {2}", filterType, filterFile, message);
            }

            executionMessage = message;
        });

    pthread_t processTid;
    p.Start().then([&ret, &processTid, jobId, taskId, requeueCount, &filterType, &filterFile] (std::pair<pid_t, pthread_t> ids)
    {
        Logger::Info(jobId, taskId, requeueCount, "{0} {1}: pid {2} tid {3}", filterType, filterFile, ids.first, ids.second);
        processTid = ids.second;
    }).wait();

    int joinRet = pthread_join(processTid, nullptr);

    if (joinRet != 0)
    {
        Logger::Error(jobId, taskId, requeueCount, "{0} {1}: join thread {2} ret {3}", filterType, filterFile, processTid, joinRet);
        if (0 == ret) { ret = joinRet; }
    }

    if (0 == ret)
    {
        std::ifstream fsStdout(stdoutFile, std::ios::in);

        if (fsStdout)
        {
            std::string content((std::istreambuf_iterator<char>(fsStdout)), std::istreambuf_iterator<char>());
            Logger::Info(jobId, taskId, requeueCount, "{0} {1}: plugin output {2}", filterType, filterFile, content);
            output = json::value::parse(content);
            fsStdout.close();
        }
        else
        {
            return (int)ErrorCodes::ReadFileError;
        }
    }

    return ret;
}
