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
#include "../utils/Logger.h"
#include "../utils/System.h"

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
                void Kill(int forcedExitCode = 0x0FFFFFFF);

            protected:
            private:
                void SetExitCode(int exitCode)
                {
                    if (!this->exitCodeSet)
                    {
                        this->exitCode = exitCode;
                        this->exitCodeSet = true;
                    }
                }

                void OnCompleted();
                void GetStatisticsFromCGroup();
                int CreateTaskFolder();

                template <typename ... Args>
                bool ExecuteCommand(const std::string& cmd, const Args& ... args)
                {
                    std::string output;
                    int ret = System::ExecuteCommand(output, cmd, args...);
                    if (ret != 0)
                    {
                        std::string cmdLine = String::Join(" ", cmd, args...);
                        this->message << "Task " << this->taskId << ": '" << cmdLine << "' failed. exitCode " << ret << "\r\n";
                        Logger::Error("Task {0}: '{1}' failed. exitCode {2}, output {3}.", this->taskId, cmdLine, ret, output);

                        this->SetExitCode(ret);

                        return false;
                    }

                    return true;
                }

                static void* ForkThread(void*);

                std::string GetAffinity();
                void Run(const std::string& path);
                void Monitor();
                std::string BuildScript();
                std::unique_ptr<const char* []> PrepareEnvironment();

                std::ostringstream stdOut;
                std::ostringstream stdErr;
                std::ostringstream message;
                int exitCode = -1;
                bool exitCodeSet = false;
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
