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
#include "../common/ErrorCodes.h"

using namespace hpc::utils;

namespace hpc
{
    namespace core
    {
        class Process
        {
            public:
                typedef void Callback(
                    int,
                    std::string&&,
                    uint64_t userTimeMs,
                    uint64_t kernelTimeMs,
                    std::vector<int>&& processIds,
                    uint64_t workingSetKb);

                Process(
                    int jobId,
                    int taskId,
                    int requeueCount,
                    const std::string& cmdLine,
                    const std::string& standardOut,
                    const std::string& standardErr,
                    const std::string& standardIn,
                    const std::string& workDir,
                    const std::string& user,
                    std::vector<uint64_t>&& cpuAffinity,
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
                int ExecuteCommand(const std::string& cmd, const Args& ... args)
                {
                    std::string output;
                    std::string cmdLine = String::Join(" ", cmd, args...);
                    int ret = System::ExecuteCommandOut(output, cmd, args...);
                    if (ret != 0)
                    {
                        this->SetExitCode(ret);
                        this->message
                            << "Task " << this->taskId << ": '" << cmdLine
                            << "', exitCode " << ret << ". output "
                            << output << std::endl;
                    }

                    Logger::Debug(this->jobId, this->taskId, this->requeueCount, "'{0}', exitCode {1}, output {2}.", cmdLine, ret, output);

                    return ret;
                }

                static void* ForkThread(void*);

                std::string GetAffinity();
                static inline void OutputAffinity(std::ostringstream& oss, int start, int end)
                {
                    if (start == end) { oss << "," << start; }
                    else if (start < end) { oss << "," << start << "-" << end; }
                }

                void Run(const std::string& path);
                void Monitor();
                std::string BuildScript();
                std::unique_ptr<const char* []> PrepareEnvironment();

                std::ostringstream stdOut;
                std::ostringstream stdErr;
                std::ostringstream message;
                int exitCode = (int)hpc::common::ErrorCodes::DefaultExitCode;
                bool exitCodeSet = false;
                uint64_t userTimeMs = 0;
                uint64_t kernelTimeMs = 0;
                std::vector<int> processIds;
                uint64_t workingSetKb;
                std::string taskFolder;

                const int jobId;
                const int taskId;
                const int requeueCount;
                const std::string taskExecutionId;
                const std::string commandLine;
                std::string stdOutFile;
                std::string stdErrFile;
                const std::string stdInFile;
                const std::string workDirectory;
                const std::string userName;
                const std::vector<uint64_t> affinity;
                const std::map<std::string, std::string> environments;
                std::vector<std::string> environmentsBuffer;

                const std::function<Callback> callback;

                pthread_t threadId;
                pid_t processId;
                bool ended = false;

                pplx::task_completion_event<pid_t> started;
        };
    }
}

#endif // PROCESS_H
