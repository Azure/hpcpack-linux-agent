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
    std::map<std::string, std::string>&& enviVars,
    std::string&& inputFiles,
    std::string&& outputFiles) :
    CommandLine(std::move(cmdLine)),
    StdInFile(std::move(stdIn)),
    StdOutFile(std::move(stdOut)),
    StdErrFile(std::move(stdErr)),
    WorkDirectory(std::move(workDir)),
    TaskRequeueCount(taskRequeueCount),
    Affinity(std::move(affinity)),
    EnvironmentVariables(std::move(enviVars)),
    InputFiles(std::move(inputFiles)),
    OutputFiles(std::move(outputFiles))
{
}

json::value ProcessStartInfo::ToJson() const
{
    json::value v;
    v["commandLine"] = json::value::string(this->CommandLine);
    v["stdin"] = json::value::string(this->StdInFile);
    v["stdout"] = json::value::string(this->StdOutFile);
    v["stderr"] = json::value::string(this->StdErrFile);
    v["workingDirectory"] = json::value::string(this->WorkDirectory);
    v["taskRequeueCount"] = this->TaskRequeueCount;
    v["affinity"] = JsonHelper<std::vector<uint64_t>>::ToJson(this->Affinity);
    v["environmentVariables"] = JsonHelper<std::map<std::string, std::string>>::ToJson(this->EnvironmentVariables);
    v["inputFiles"] = json::value::string(this->InputFiles);
    v["outputFiles"] = json::value::string(this->OutputFiles);
    return v;
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
        JsonHelper<std::map<std::string, std::string>>::Read("environmentVariables", jsonValue),
        JsonHelper<std::string>::Read("inputFiles", jsonValue),
        JsonHelper<std::string>::Read("outputFiles", jsonValue));

    return std::move(startInfo);
}

