#ifndef PROCESS_H
#define PROCESS_H

#include <string>
#include <sstream>
#include <vector>
#include <map>
#include <memory>
#include <unistd.h>
#include <sys/signal.h>
#include <pplx/pplxtasks.h>
#include <sys/wait.h>
#include <sys/resource.h>

#include "../utils/String.h"

using namespace hpc::utils;

namespace hpc
{
    namespace core
    {
        class Process
        {
            public:
                typedef void Callback(int, std::string&&, timeval userTime, timeval kernelTime);

                Process(
                    int taskId,
                    const std::string& cmdLine,
                    const std::string& standardOut,
                    const std::string& standardErr,
                    const std::string& standardIn,
                    const std::string& workDir,
                    std::vector<long>&& cpuAffinity,
                    std::map<std::string, std::string>&& envi,
                    const std::function<Callback> completed);

                Process(Process&&) = default;

                virtual ~Process();

                pplx::task<pid_t> Start();
                void Kill();

            protected:
            private:
                void OnCompleted();
                int CreateTaskFolder();
                int DeleteTaskFolder();
                static void* ForkThread(void*);
                static void ReadFromPipe(std::ostringstream& stream, int pipe[2]);

                template <typename ... Args>
                static int ExecuteCommand(std::string& output, const std::string& cmd, const Args& ... args)
                {
                    std::string command = String::Join(" ", cmd, args...);
                    FILE* stream = popen(command.c_str(), "r");

                    std::ostringstream result;
                    int exitCode = -1;

                    if (stream)
                    {
                        char buffer[512];
                        while (fgets(buffer, sizeof(buffer), stream) != nullptr)
                        {
                            result << buffer;
                        }

                        int ret = pclose(stream);
                        exitCode = WEXITSTATUS(ret);
                    }
                    else
                    {
                        result << "error when popen " << command << std::endl;
                    }

                    output = result.str();

                    return exitCode;
                }

                void Run(const std::string& path);
                void Monitor();
                std::string BuildScript();
                void CleanupScript(const std::string& path);
                std::unique_ptr<const char* []> PrepareEnvironment();

                std::ostringstream stdOut;
                std::ostringstream stdErr;
                std::ostringstream message;
                int exitCode;
                timeval userTime = { 0, 0 };
                timeval kernelTime = { 0, 0 };
                std::string taskFolder;

                const int taskId;
                const std::string commandLine;
                std::string stdOutFile;
                std::string stdErrFile;
                const std::string stdInFile;
                const std::string workDirectory;
                const std::vector<long> affinity;
                const std::map<std::string, std::string> environments;
                std::vector<std::string> environmentsBuffer;

                const std::function<Callback> callback;

                pthread_t threadId;
                pid_t processId;

                pplx::task_completion_event<pid_t> started;
        };
    }
}

#endif // PROCESS_H
