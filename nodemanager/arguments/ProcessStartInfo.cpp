#include "ProcessStartInfo.h"
#include "../utils/JsonHelper.h"

using namespace hpc::arguments;
using namespace hpc::utils;

ProcessStartInfo::ProcessStartInfo(
    std::string&& cmdLine,
    std::string&& workDir,
    std::vector<long>&& affinity,
    std::map<std::string, std::string>&& enviVars) :
    CommandLine(std::move(cmdLine)), WorkDirectory(std::move(workDir)), Affinity(std::move(affinity)), EnvironmentVariables(std::move(enviVars))
{
}

/// TODO: Consider using attribute feature when compiler ready.
ProcessStartInfo ProcessStartInfo::FromJson(const web::json::value& jsonValue)
{
    ProcessStartInfo startInfo(
        JsonHelper<std::string>::Read("commandLine", jsonValue),
        JsonHelper<std::string>::Read("workingDirectory", jsonValue),
        JsonHelper<std::vector<long>>::Read("affinity", jsonValue),
        JsonHelper<std::map<std::string, std::string>>::Read("environmentVariables", jsonValue));

    return std::move(startInfo);
}

