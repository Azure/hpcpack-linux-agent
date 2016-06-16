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

pplx::task<json::value> ExecutionFilter::OnJobStart(int jobId, int taskId, int requeueCount, const json::value& input)
{
    return this->ExecuteFilter(JobStartFilter, jobId, taskId, requeueCount, input);
}

pplx::task<json::value> ExecutionFilter::OnJobEnd(int jobId, const json::value& input)
{
    return this->ExecuteFilter(JobEndFilter, jobId, 0, 0, input);
}

pplx::task<json::value> ExecutionFilter::OnTaskStart(int jobId, int taskId, int requeueCount, const json::value& input)
{
    return this->ExecuteFilter(TaskStartFilter, jobId, taskId, requeueCount, input);
}

pplx::task<json::value> ExecutionFilter::ExecuteFilter(const std::string& filterType, int jobId, int taskId, int requeueCount, const json::value& input)
{
    auto filterIt = this->filterFiles.find(filterType);
    if (filterIt == this->filterFiles.end())
    {
        Logger::Error(jobId, taskId, requeueCount, "Unknown filter type {0}", filterType);
        throw std::runtime_error(String::Join(" ", "Unknown filter type", filterType));
    }

    std::string filterFile = filterIt->second;

    std::ifstream test(filterFile);
    if (!test.good())
    {
        Logger::Info(jobId, taskId, requeueCount, "{0} not detected, skip", filterFile);
        return pplx::task_from_result(input);
    }

    if (filterFile[0] != '/')
    {
        std::string pwd;
        System::ExecuteCommandOut(pwd, "pwd | tr -d '\n'");
        filterFile = pwd + "/" + filterFile;
    }

    char folder[256];
    sprintf(folder, "/tmp/nodemanager_executionfilter_%d.XXXXXX", jobId);
    int ret = System::CreateTempFolder(folder, "root");

    if (ret != 0)
    {
        Logger::Error(jobId, taskId, requeueCount, "{0} {1}: Failed to create folder {2}, exit code {3}", filterType, filterFile, folder, ret);
        throw std::runtime_error(String::Join("", filterType, " ", filterFile, ": Failed to create folder ", folder, ", ret ", ret));
    }

    std::string folderString = folder;

    std::string stdinFile = folderString + "/stdin.txt";
    ret = System::WriteStringToFile(stdinFile, input.serialize());

    if (ret != 0)
    {
        Logger::Error(jobId, taskId, requeueCount, "{0} {1}: Failed to create stdin file {2}, exit code {3}", filterType, filterFile, stdinFile, ret);
        throw std::runtime_error(String::Join("", filterType, " ", filterFile, ": Failed to create stdin file ", stdinFile, ", exit code ", ret));
    }

    std::string stdoutFile = folderString + "/stdout.txt";
    std::string stderrFile = stdoutFile;

    Process* p = new Process(
        jobId, taskId, requeueCount, filterFile, stdoutFile, stderrFile, stdinFile, folderString, "root",
        std::vector<uint64_t>(), std::map<std::string, std::string>(),
        [=] (int exitCode, std::string&& message, const ProcessStatistics& stat)
        {
            if (exitCode != 0)
            {
                Logger::Error(jobId, taskId, requeueCount, "{0} {1}: returned {2}, message {3}", filterType, filterFile, exitCode, message);
            }
            else
            {
                Logger::Info(jobId, taskId, requeueCount, "{0} {1}: success with message {2}", filterType, filterFile, message);
            }
        });

    p->Start().then([=] (std::pair<pid_t, pthread_t> ids)
    {
        Logger::Info(jobId, taskId, requeueCount, "{0} {1}: pid {2} tid {3}", filterType, filterFile, ids.first, ids.second);
    });

    return p->OnCompleted().then([=] (pplx::task<void> t)
    {
        int ret = p->GetExitCode();
        std::string executionMessage = p->GetExecutionMessage();
        delete p;
        t.get();

        if (0 == ret)
        {
            std::ifstream fsStdout(stdoutFile, std::ios::in);

            json::value output;
            if (fsStdout)
            {
                std::string content((std::istreambuf_iterator<char>(fsStdout)), std::istreambuf_iterator<char>());
                Logger::Info(jobId, taskId, requeueCount, "{0} {1}: plugin output {2}", filterType, filterFile, content);
                output = json::value::parse(content);
                fsStdout.close();

                return output;
            }
            else
            {
                throw std::runtime_error(String::Join("", filterType, " ", filterFile, ": Unable to read stdout file ", stdoutFile, ", exit code ", (int)ErrorCodes::ReadFileError));
            }
        }

        throw std::runtime_error(String::Join("", filterType, " ", filterFile, ": Filter returned exit code ", ret, ", execution message ", executionMessage));
    });
}
