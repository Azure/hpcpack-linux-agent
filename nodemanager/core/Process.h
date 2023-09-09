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
#include <boost/algorithm/string.hpp>

#include "../utils/String.h"
#include "../utils/Logger.h"
#include "../utils/System.h"
#include "../common/ErrorCodes.h"
#include "../data/ProcessStatistics.h"

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
                    const hpc::data::ProcessStatistics& stat);

                Process(
                    int jobId,
                    int taskId,
                    int requeueCount,
                    const std::string& taskExecutionName,
                    const std::string& cmdLine,
                    const std::string& standardOut,
                    const std::string& standardErr,
                    const std::string& standardIn,
                    const std::string& workDir,
                    const std::string& user,
                    bool dumpStdoutToExecutionMessage,
                    std::vector<uint64_t>&& cpuAffinity,
                    std::map<std::string, std::string>&& envi,
                    const std::function<Callback> completed);

                Process(Process&&) = default;

                virtual ~Process();

                pplx::task<std::pair<pid_t, pthread_t>> Start(std::shared_ptr<Process> self);
                void Kill(int forcedExitCode = 0x0FFFFFFF, bool forced = true);
                const hpc::data::ProcessStatistics& GetStatisticsFromCGroup();

                static void Cleanup();

                pplx::task<void> OnCompleted();

                int GetExitCode() const { return this->exitCode; }
                std::string GetExecutionMessage() const { return this->message.str(); }

                void SetSelfPtr(std::shared_ptr<Process> self) { this->selfPtr.swap(self); }
                void ResetSelfPtr() { this->selfPtr.reset(); }

                std::string PeekOutput();

            protected:
            private:
                static bool StartWithHttpOrHttps(const std::string& path)
                {
                    return boost::algorithm::starts_with(path, "http://") || boost::algorithm::starts_with(path, "https://");
                }

                void SetExitCode(int exitCode)
                {
                    this->exitCode = exitCode;
                    this->exitCodeSet = true;
                }

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

                template <typename ... Args>
                int ExecuteCommandNoCapture(const std::string& cmd, const Args& ... args)
                {
                    std::string output;
                    std::string cmdLine = String::Join(" ", cmd, args...);
                    int ret = System::ExecuteCommandOut(output, cmd, args...);

                    Logger::Debug(this->jobId, this->taskId, this->requeueCount, "'{0}', exitCode {1}, output {2}.", cmdLine, ret, output);

                    return ret;
                }

                static void* ForkThread(void*);

                std::string GetAffinity();

                void Run(const std::string& path);
                static void* ReadPipeThread(void* p);
                void SendbackOutput(const std::string& uri, const std::string& output, int order) const;
                void Monitor();
                std::string BuildScript();
                std::unique_ptr<const char* []> PrepareEnvironment();
                void OnCompletedInternal();

                std::ostringstream stdOut;
                std::ostringstream stdErr;
                std::ostringstream message;
                int exitCode = (int)hpc::common::ErrorCodes::DefaultExitCode;
                bool exitCodeSet = false;

                hpc::data::ProcessStatistics statistics;

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
                bool dumpStdout = false;
                const std::vector<uint64_t> affinity;
                const std::map<std::string, std::string> environments;
                std::vector<std::string> environmentsBuffer;
                bool streamOutput = false;
                int stdoutPipe[2];

                const std::function<Callback> callback;

                std::shared_ptr<Process> selfPtr;

                pthread_t threadId = 0;
                pthread_t outputThreadId = 0;
                pid_t processId;
                bool ended = false;

                pthread_rwlock_t lock = PTHREAD_RWLOCK_INITIALIZER;

                pplx::task_completion_event<std::pair<pid_t, pthread_t>> started;
                pplx::task_completion_event<void> completed;
        };
    }
}

#endif // PROCESS_H
