#ifndef PROCESS_H
#define PROCESS_H

#include <string>
#include <sstream>
#include <vector>
#include <map>
#include <memory>
#include <unistd.h>
#include <sys/signal.h>

namespace hpc
{
    namespace core
    {
        class Process
        {
            public:
                typedef void Callback(int, std::string&&, std::string&&, std::string&&);

                Process(
                    const std::string& cmdLine,
                    const std::vector<std::string>& args,
                    const std::map<std::string, std::string>& envi,
                    const std::function<Callback> completed);

                virtual ~Process();

                void Start();
                void Kill();

            protected:
            private:
                void OnCompleted();
                static void* ForkThread(void*);
                void Run(int stdOutPipe[2], int stdErrPipe[2]);
                void Monitor();
                const std::string& BuildScript();
                void CleanupScript();
                std::unique_ptr<char* const []> PrepareEnvironment();

                std::ostringstream stdOut;
                std::ostringstream stdErr;
                std::ostringstream message;
                int exitCode;

                const std::string& commandLine;
                const std::vector<std::string>& arguments;
                const std::map<std::string, std::string>& environments;
                const std::vector<std::string> environmentsBuffer;
                const std::function<Callback> callback;

                pthread_t* threadId;
                pid_t processId;

                std::string scriptPath;
        };
    }
}

#endif // PROCESS_H
