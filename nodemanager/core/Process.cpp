#include "Process.h"
#include "../utils/Logger.h"

using namespace hpc::core;
using namespace hpc::utils;

Process::Process(
    const std::string& cmdLine,
    const std::vector<std::string>& args,
    const std::map<std::string, std::string>& envi,
    const std::function<Callback> completed) :
    commandLine(cmdLine), arguments(args), environments(envi), callback(completed),
    threadId(nullptr), processId(0)
{

}

Process::~Process()
{
    this->Kill();
    if (this->threadId)
    {
        delete this->threadId;
        this->threadId = nullptr;
    }
}

void Process::Start()
{
    this->threadId = new pthread_t();
    pthread_create(this->threadId, nullptr, ForkThread, this);

    Logger::Info("Created thread {0}", *this->threadId);
}

void Process::Kill()
{
    if (this->processId != 0) { kill(this->processId, SIGQUIT); }
}

void Process::OnCompleted()
{
    try
    {
        this->callback(this->exitCode, std::move(this->stdOut.str()), std::move(this->stdErr.str()), std::move(this->message.str()));
    }
    catch (const std::exception& ex)
    {
        Logger::Error("Exception happened when callback, ex = {0}", ex.what());
    }
    catch (...)
    {
        Logger::Error("Unknown exception happened when callback");
    }
}

void* Process::ForkThread(void* arg)
{
    Process* const p = static_cast<Process* const>(arg);

    int stdOutPipe[2], stdErrPipe[2];

    if (-1 == pipe(stdOutPipe))
    {
        p->message << "Error when creating stdout pipe, errno = " << errno << std::endl;
        Logger::Error("Error when creating stdout pipe, errno = {0}", errno);

        p->exitCode = errno;
        goto Final;
    }

    if (-1 == pipe(stdErrPipe))
    {
        p->message << "Error when creating stderr pipe, errno = " << errno << std::endl;
        Logger::Error("Error when creating stderr pipe, errno = {0}", errno);

        p->exitCode = errno;
        goto Final;
    }

    p->processId = fork();

    if (p->processId < 0)
    {
        std::string errorMessage =
            errno == EAGAIN ? "number of process reached upper limit" : "not enough memory";

        p->message << "Failed to fork(), pid = " << p->processId << ", errno = " << errno
            << ", msg = " << errorMessage << std::endl;
        Logger::Error("Failed to fork(), pid = {0}, errno = {1}, msg = {2}", p->processId, errno, errorMessage);

        p->exitCode = errno;
        goto Final;
    }

    if (p->processId == 0)
    {
        p->Run(stdOutPipe, stdErrPipe);
    }
    else
    {
        p->Monitor();
    }

Final:
    p->OnCompleted();
    pthread_exit(0);
}

void Process::Monitor()
{

}

void Process::Run(int stdOutPipe[2], int stdErrPipe[2])
{
    const std::string& path = this->BuildScript();

    std::vector<char> pathBuffer(path.cbegin(), path.cend());
    pathBuffer.push_back('\0');

    char* const bashCmd = const_cast<char* const>("/bin/bash");

    char* const args[3] = { bashCmd, &pathBuffer[0], nullptr };

    auto envi = this->PrepareEnvironment();

    dup2(stdOutPipe[1], 1);
    close(stdOutPipe[0]);
    close(stdOutPipe[1]);
    dup2(stdErrPipe[1], 2);
    close(stdOutPipe[0]);
    close(stdOutPipe[1]);

    int ret = execvpe(bashCmd, args, envi.get());

    assert(ret == -1);

    int err = errno;
    std::cout << "Error occurred when execvpe, errno = " << err << std::endl;

    this->CleanupScript();

    exit(err);
}

const std::string& Process::BuildScript()
{
    return this->scriptPath;
}

void Process::CleanupScript()
{
}

std::unique_ptr<char * const []> Process::PrepareEnvironment()
{
    return nullptr;
}
