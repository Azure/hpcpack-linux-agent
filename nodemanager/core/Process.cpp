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
    const std::string& standardOut,
    const std::string& standardErr,
    const std::string& standardIn,
    const std::string& workDir,
    std::vector<long>&& cpuAffinity,
    std::map<std::string, std::string>&& envi,
    const std::function<Callback> completed) :
    taskId(taskId),
    commandLine(cmdLine), stdOutFile(standardOut), stdErrFile(standardErr), stdInFile(standardIn),
    workDirectory(workDir), affinity(cpuAffinity), environments(envi), callback(completed), processId(0)
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
    if (this->processId != 0)
    {
        std::string output;
        int ret = Process::ExecuteCommand(output, "/bin/bash", "EndTask.sh", this->taskId);

        if (0 != ret)
        {
            Logger::Error("EndTask {0}, exitCode {1}, output {2}", this->taskId, ret, output);
        }
        else
        {
            Logger::Info("EndTask {0}, output {1}", this->taskId, output);
        }

//        if (-1 == kill(this->processId, SIGKILL))
//        {
//            Logger::Error("Process {0}: Kill errno {1}", this->processId, errno);
//            this->message << "Process " << this->processId << ": Kill errno " << errno << "\r\n";
//        }
//
//        Logger::Info("Process {0}: killed", this->processId);
        this->processId = 0;
    }
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
    std::string path;

    int ret = p->CreateTaskFolder();
    if (ret != 0)
    {
        p->message << "Task " << p->taskId << ": error when create task folder, ret " << ret << ". \r\n";
        Logger::Error("Task {0}: error when create task folder, ret {1}", p->taskId, ret);

        // TODO fetch the errno.
        p->exitCode = ret;
        goto Final;
    }

    path = p->BuildScript();

    if (path.empty())
    {
        p->message << "Error when build script. \r\n";
        Logger::Error("Error when build script.");

        // TODO fetch the errno.
        p->exitCode = -1;
        goto Final;
    }
//
//    int stdOutPipe[2], stdErrPipe[2];
//
//    if (-1 == pipe(stdOutPipe))
//    {
//        p->message << "Error when creating stdout pipe, errno = " << errno << "\r\n";
//        Logger::Error("Error when creating stdout pipe, errno = {0}", errno);
//
//        p->exitCode = errno;
//        goto Final;
//    }
//
//    if (-1 == pipe(stdErrPipe))
//    {
//        p->message << "Error when creating stderr pipe, errno = " << errno << "\r\n";
//        Logger::Error("Error when creating stderr pipe, errno = {0}", errno);
//
//        p->exitCode = errno;
//        goto Final;
//    }

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
        p->Run(path);
    }
    else
    {
        p->started.set(p->processId);
        p->Monitor();
    }

Final:
    p->processId = 0;
    p->CleanupScript(path);
    p->DeleteTaskFolder();
    p->OnCompleted();

    pthread_exit(nullptr);
}

void Process::Monitor()
{
    Logger::Debug("Monitor the forked process {0}", this->processId);

    int status;
    rusage usage;
    pid_t waitedPid = wait4(this->processId, &status, 0, &usage);
    if (waitedPid == -1)
    {
        Logger::Error("wait4 for process {0} error {1}", this->processId, errno);
        // TODO: move "\r\n" to a common place
        this->message << "wait4 for process " << this->processId << " error " << errno << "\r\n";
        this->exitCode = errno;
        return;
    }

    if (waitedPid != this->processId)
    {
        Logger::Error("Process {0}: waited {1}, errno {2}", this->processId, waitedPid, errno);

        int tmp;
        if (WIFEXITED(status)) Logger::Info("Process {0}: WIFEXITED", this->processId);
        if ((tmp = WEXITSTATUS(status))) Logger::Info("Process {0}: WEXITSTATUS: {1}", this->processId, tmp);
        if (WIFSIGNALED(status)) Logger::Info("Process {0}: WIFSIGNALED", this->processId);
        if ((tmp = WTERMSIG(status))) Logger::Info("Process {0}: WTERMSIG: {1}", this->processId, tmp);
        if (WCOREDUMP(status)) Logger::Info("Process {0}: Core dumped.", this->processId);
        if (WIFSTOPPED(status)) Logger::Info("Process {0}: WIFSTOPPED", this->processId);
        if (WSTOPSIG(status)) Logger::Info("Process {0}: WSTOPSIG", this->processId);
        if (WIFCONTINUED(status)) Logger::Info("Process {0}: WIFCONTINUED", this->processId);

        this->message << "Process " << this->processId << ": waited " << waitedPid << ", errno " << errno << "\r\n";
        this->exitCode = errno;
        return;
    }

    if (WIFEXITED(status))
    {
        this->exitCode = WEXITSTATUS(status);


        std::string output;
        int ret = Process::ExecuteCommand(output, "head -c 1024", this->stdOutFile);
        if (ret == 0)
        {
            this->message << "STDOUT: " << output << "\r\n";
        }

        ret = Process::ExecuteCommand(output, "head -c 1024", this->stdErrFile);
        if (ret == 0)
        {
            this->message << "STDERR: " << output << "\r\n";
        }
//
//        ReadFromPipe(this->stdOut, stdOutPipe);
//        ReadFromPipe(this->stdErr, stdErrPipe);
//
//        this->message << this->stdOut.str() << "\r\n";
//        this->message << this->stdErr.str() << "\r\n";
    }
    else
    {
        Logger::Error("wait4 for process {0} status {1}", this->processId, status);
        this->exitCode = -1;
        this->message << "wait4 for process " << this->processId << " status " << status << "\r\n";
    }

    this->userTime = usage.ru_utime;
    this->kernelTime = usage.ru_stime;

    Logger::Debug("Process {0}: Monitor ended", this->processId);
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

void Process::Run(const std::string& path)
{
    std::vector<char> pathBuffer(path.cbegin(), path.cend());
    pathBuffer.push_back('\0');

    char* const bashCmd = const_cast<char* const>("/bin/bash");

    char* const args[3] = { bashCmd, &pathBuffer[0], nullptr };

    auto envi = this->PrepareEnvironment();

//    dup2(stdOutPipe[1], 1);
//    close(stdOutPipe[0]);
//    close(stdOutPipe[1]);
//    dup2(stdErrPipe[1], 2);
//    close(stdOutPipe[0]);
//    close(stdOutPipe[1]);

    int ret = execvpe(bashCmd, args, const_cast<char* const*>(envi.get()));

    assert(ret == -1);

    std::cout << "Error occurred when execvpe, errno = " << errno << "\r\n";

    exit(errno);
}

int Process::CreateTaskFolder()
{
    char folder[256];

    sprintf(folder, "/tmp/nodemanager_task_%d.XXXXXX", this->taskId);

    char* p = mkdtemp(folder);

    if (p)
    {
        this->taskFolder = p;
        return 0;
    }
    else
    {
        return errno;
    }
}

int Process::DeleteTaskFolder()
{
    std::string output;
    int ret = Process::ExecuteCommand(output, "rm -rf", this->taskFolder);
    if (ret != 0)
    {
        Logger::Error("Task {0}: Unable to dir -rf {1}, ret {2}", this->taskId, this->taskFolder, ret);
        this->message << "Task " << this->taskId << ": Unable to rmdir -rf " << this->taskFolder << ", ret " << ret << "\r\n";
    }

    return ret;
}

std::string Process::BuildScript()
{
    std::string path = this->taskFolder + "/run.sh";

    std::ofstream fs(path, std::ios::trunc);
    fs << "#!/bin/bash" << std::endl << std::endl;

    fs << "cd ";
    if (this->workDirectory.empty() || access(this->workDirectory.c_str(), 0) == -1)
    {
        fs << this->taskFolder;
    }
    else
    {
        fs << this->workDirectory;
    }

    fs << std::endl << std::endl;

    if (this->stdOutFile.empty()) this->stdOutFile = this->taskFolder + "/stdout.txt";
    if (this->stdErrFile.empty()) this->stdErrFile = this->taskFolder + "/stderr.txt";

    if (!this->commandLine.empty())
    {
        fs << this->commandLine
            << " >" << this->stdOutFile
            << " 2>" << this->stdErrFile;
    }

    if (!this->stdInFile.empty())
    {
        fs << " <" << this->stdInFile;
    }

    fs << std::endl << std::endl;

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
