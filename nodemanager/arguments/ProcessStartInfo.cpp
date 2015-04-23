#include "ProcessStartInfo.h"
#include "../utils/JsonHelper.h"

using namespace hpc::arguments;
using namespace hpc::utils;

ProcessStartInfo::ProcessStartInfo(
    std::string&& cmdLine,
    std::string&& stdIn,
    std::string&& stdOut,
    std::string&& stdErr,
    std::string&& workDir,
    int taskRequeueCount,
    std::vector<uint64_t>&& affinity,
    std::map<std::string, std::string>&& enviVars) :
    CommandLine(std::move(cmdLine)),
    StdInFile(std::move(stdIn)),
    StdOutFile(std::move(stdOut)),
    StdErrFile(std::move(stdErr)),
    WorkDirectory(std::move(workDir)),
    TaskRequeueCount(taskRequeueCount),
    Affinity(std::move(affinity)),
    EnvironmentVariables(std::move(enviVars))
{
}

/// TODO: Consider using attribute feature when compiler ready.
ProcessStartInfo ProcessStartInfo::FromJson(const web::json::value& jsonValue)
{
    ProcessStartInfo startInfo(
        JsonHelper<std::string>::Read("commandLine", jsonValue),
        JsonHelper<std::string>::Read("stdin", jsonValue),
        JsonHelper<std::string>::Read("stdout", jsonValue),
        JsonHelper<std::string>::Read("stderr", jsonValue),
        JsonHelper<std::string>::Read("workingDirectory", jsonValue),
        JsonHelper<int>::Read("taskRequeueCount", jsonValue),
        JsonHelper<std::vector<uint64_t>>::Read("affinity", jsonValue),
        JsonHelper<std::map<std::string, std::string>>::Read("environmentVariables", jsonValue));

    return std::move(startInfo);
}

