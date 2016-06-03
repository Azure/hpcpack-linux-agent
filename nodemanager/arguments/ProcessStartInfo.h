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
                    std::vector<uint64_t>&& affinity,
                    std::map<std::string, std::string>&& enviVars);

                ProcessStartInfo(ProcessStartInfo&& startInfo) = default;

                static ProcessStartInfo FromJson(const web::json::value& jsonValue);
                web::json::value ToJson() const;

                std::string CommandLine;
                std::string StdInFile;
                std::string StdOutFile;
                std::string StdErrFile;
                std::string WorkDirectory;
                int TaskRequeueCount;
                std::vector<uint64_t> Affinity;
                std::map<std::string, std::string> EnvironmentVariables;
            protected:
            private:
        };
    }
}
#endif // PROCESSSTARTINFO_H
