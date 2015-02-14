#include "ProcessStartInfo.h"
#include "../utils/JsonHelper.h"

namespace hpc
{
    namespace arguments
    {
        using namespace hpc::utils;

        ProcessStartInfo::ProcessStartInfo(
            const std::string& cmdLine,
            const std::string& workingDir,
            std::vector<long>&& affinity,
            std::map<std::string, std::string>&& enviVars) :
            CommandLine(cmdLine), WorkingDirectory(workingDir), Affinity(affinity), EnvironmentVariables(enviVars)
        {
        }

        /// TODO: Consider using attribute feature when compiler ready.
        ProcessStartInfo&& ProcessStartInfo::FromJson(const web::json::value& jsonValue)
        {
            ProcessStartInfo startInfo(
                JsonHelper<std::string>::Read("commandLine", jsonValue),
                JsonHelper<std::string>::Read("workingDirectory", jsonValue),
                JsonHelper<std::vector<long>>::Read("affinity", jsonValue),
                JsonHelper<std::map<std::string, std::string>>::Read("environmentVariables", jsonValue));

            return std::move(startInfo);
        }
    }
}
