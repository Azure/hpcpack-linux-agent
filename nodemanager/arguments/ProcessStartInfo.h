#ifndef PROCESSSTARTINFO_H
#define PROCESSSTARTINFO_H

#include <string>
#include <vector>
#include <map>

#include <cpprest/json.h>

namespace hpc
{
    namespace arguments
    {
        struct ProcessStartInfo
        {
            public:
                ProcessStartInfo(
                    const std::string& cmdLine,
                    const std::string& workingDir,
                    std::vector<long>&& affinity,
                    std::map<std::string, std::string>&& enviVars);

                ProcessStartInfo(ProcessStartInfo&& startInfo) :
                    CommandLine(startInfo.CommandLine),
                    WorkingDirectory(startInfo.WorkingDirectory),
                    Affinity(std::move(startInfo.Affinity)),
                    EnvironmentVariables(std::move(startInfo.EnvironmentVariables))
                {

                }

                static ProcessStartInfo&& FromJson(const web::json::value& jsonValue);

                std::string CommandScript;
                std::string CommandLine;
                std::string StdInText;
                std::string StdOutText;
                std::string StdErrText;
                std::string WorkingDirectory;
                std::vector<long> Affinity;
                std::map<std::string, std::string> EnvironmentVariables;
            protected:
            private:
        };
    }
}
#endif // PROCESSSTARTINFO_H
