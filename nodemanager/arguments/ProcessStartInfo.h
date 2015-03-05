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
                    std::string&& cmdLine,
                    std::string&& stdIn,
                    std::string&& stdOut,
                    std::string&& stdErr,
                    std::string&& workDir,
                    int taskRequeueCount,
                    std::vector<long>&& affinity,
                    std::map<std::string, std::string>&& enviVars);

                ProcessStartInfo(ProcessStartInfo&& startInfo) = default;

                static ProcessStartInfo FromJson(const web::json::value& jsonValue);

                std::string CommandLine;
                std::string StdInText;
                std::string StdOutText; // TODO: use stdout
                std::string StdErrText; // TODO: use stderr
                std::string WorkDirectory;
                int TaskRequeueCount;
                std::vector<long> Affinity; // TODO: use the affinity
                std::map<std::string, std::string> EnvironmentVariables;
            protected:
            private:
        };
    }
}
#endif // PROCESSSTARTINFO_H
