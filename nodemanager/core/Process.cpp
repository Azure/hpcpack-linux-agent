#include <memory.h>
#include <sys/wait.h>
#include <sys/resource.h>
#include <fstream>

#include "Process.h"
#include "../utils/Logger.h"
#include "../utils/String.h"

using namespace hpc::core;
using namespace hpc::utils;

Process::Process(
    int taskId,
    const std::string& cmdLine,
    const std::string& workDir,
    std::map<std::string, std::string>&& envi,
    const std::function<Callback> completed) :
    taskId(taskId),
    commandLine(cmdLine), workDirectory(workDir),
    environments(envi), callback(completed), processId(0)
{

}

Process::~Process()
{
    this->Kill();
}

pplx::task<pid_t> Process::Start()
{
    pthread_create(&this->threadId, nullptr, ForkThread, this);

    Logger::Debug("Created thread {0}", this->threadId);

    return pplx::task<pid_t>(this->started);
}

void Process::Kill()
{
    if (this->processId != 0) { kill(this->processId, SIGQUIT); this->processId = 0; }
}

void Process::OnCompleted()
{
    try
    {
        this->callback(this->exitCode, this->message.str(), this->userTime, this->kernelTime);
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

    const std::string path = p->BuildScript();

    int stdOutPipe[2], stdErrPipe[2];

    if (-1 == pipe(stdOutPipe))
    {
        p->message << "Error when creating stdout pipe, errno = " << errno << "\r\n";
        Logger::Error("Error when creating stdout pipe, errno = {0}", errno);

        p->exitCode = errno;
        goto Final;
    }

    if (-1 == pipe(stdErrPipe))
    {
        p->message << "Error when creating stderr pipe, errno = " << errno << "\r\n";
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
            << ", msg = " << errorMessage << "\r\n";
        Logger::Error("Failed to fork(), pid = {0}, errno = {1}, msg = {2}", p->processId, errno, errorMessage);

        p->exitCode = errno;
        goto Final;
    }

    if (p->processId == 0)
    {
        p->Run(stdOutPipe, stdErrPipe, path);
    }
    else
    {
        p->started.set(p->processId);
        p->Monitor(stdOutPipe, stdErrPipe);
    }

Final:
    p->processId = 0;
    p->CleanupScript(path);

    p->OnCompleted();

    pthread_exit(nullptr);
}

void Process::Monitor(int stdOutPipe[2], int stdErrPipe[2])
{
    Logger::Debug("Monitor the forked process {0}", this->processId);

    int status;
    rusage usage;
    pid_t waitedPid = wait4(this->processId, &status, 0, &usage);
    if (waitedPid == -1)
    {
        Logger::Error("wait4 for process {0} error {1}", this->processId, errno);
        // TODO: move "\r\n" to a common place
        this->message << "wait4 for process " << " error " << errno << "\r\n";
        this->exitCode = errno;
        return;
    }

    assert(this->processId == waitedPid);

    if (WIFEXITED(status))
    {
        this->exitCode = WEXITSTATUS(status);
    }
    else
    {
        Logger::Error("wait4 for process {0} status {1}", this->processId, status);
        this->exitCode = -1;
        this->message << "wait4 for process " << this->processId << " status " << status << "\r\n";
    }

    ReadFromPipe(this->stdOut, stdOutPipe);
    ReadFromPipe(this->stdErr, stdErrPipe);

    this->message << this->stdOut.str() << "\r\n";
    this->message << this->stdErr.str() << "\r\n";

    this->userTime = usage.ru_utime;
    this->kernelTime = usage.ru_stime;
    // TODO: Number of process;
    // TODO: ProcessIds
    // TODO: WorkingSet
}

void Process::ReadFromPipe(std::ostringstream& stream, int pipe[2])
{
    close(pipe[1]);

    ssize_t bytesRead;
    const int BufferSize = 1024;
    char buf[BufferSize] = { 0 };

    while ((bytesRead = read(pipe[0], buf, sizeof(buf) - 1)) > 0)
    {
        buf[bytesRead] = 0;
        stream << buf;
    }

    close(pipe[0]);
}

void Process::Run(int stdOutPipe[2], int stdErrPipe[2], const std::string& path)
{
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

    int ret = execvpe(bashCmd, args, const_cast<char* const*>(envi.get()));

    assert(ret == -1);

    std::cout << "Error occurred when execvpe, errno = " << errno << "\r\n";

    exit(errno);
}

std::string Process::BuildScript()
{
    char scriptPath[256];

    sprintf(scriptPath, "/tmp/nodemanager_task_%d.XXXXXX", this->taskId);

    int fd;
    if ((fd = mkstemp(scriptPath)) < 0)
    {
        return std::string();
    }
    else
    {
        close(fd);
    }

    std::string path = scriptPath;
    Logger::Debug("Script path {0}", path);

    std::ofstream fs(path, std::ios::trunc);
    fs << "#!/bin/bash" << std::endl << std::endl;
    if (!this->workDirectory.empty() && access(this->workDirectory.c_str(), 0) != -1)
    {
        fs << "cd " << this->workDirectory << std::endl << std::endl;
    }

    if (!this->commandLine.empty())
    {
        fs << this->commandLine << std::endl;
    }

    fs.close();

    return std::move(path);
}

void Process::CleanupScript(const std::string& path)
{
    if (!path.empty())
    {
        unlink(path.c_str());
    }
}

std::unique_ptr<const char* []> Process::PrepareEnvironment()
{
    std::transform(
        this->environments.cbegin(),
        this->environments.cend(),
        std::back_inserter(this->environmentsBuffer),
        [](const auto& v) { return String::Join("=", v.first, v.second); });

    auto envi = std::unique_ptr<const char* []>(new const char*[this->environments.size() + 1]);
    int p = 0;
    for_each(
        this->environmentsBuffer.cbegin(),
        this->environmentsBuffer.cend(),
        [&p, &envi](const auto& i) { envi[p++] = i.c_str(); });

    envi[p] = nullptr;

    return std::move(envi);
}
