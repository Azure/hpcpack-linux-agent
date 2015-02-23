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

namespace hpc
{
    namespace core
    {
        class Process
        {
            public:
                typedef void Callback(int, std::string&&, timeval userTime, timeval kernelTime);

                Process(
                    const std::string& cmdLine,
                    const std::string& workDir,
                    std::map<std::string, std::string>&& envi,
                    const std::function<Callback> completed);

                Process(Process&&) = default;

                virtual ~Process();

                pplx::task<pid_t> Start();
                void Kill();

            protected:
            private:
                void OnCompleted();
                static void* ForkThread(void*);
                static void ReadFromPipe(std::ostringstream& stream, int pipe[2]);
                void Run(int stdOutPipe[2], int stdErrPipe[2], const std::string& path);
                void Monitor(int stdOutPipe[2], int stdErrPipe[2]);
                std::string BuildScript();
                void CleanupScript(const std::string& path);
                std::unique_ptr<const char* []> PrepareEnvironment();

                std::ostringstream stdOut;
                std::ostringstream stdErr;
                std::ostringstream message;
                int exitCode;
                timeval userTime;
                timeval kernelTime;

                const std::string commandLine;
                const std::string workDirectory;
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
