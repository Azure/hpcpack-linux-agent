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
    int jobId,
    int taskId,
    int requeueCount,
    const std::string& cmdLine,
    const std::string& standardOut,
    const std::string& standardErr,
    const std::string& standardIn,
    const std::string& workDir,
    std::vector<long>&& cpuAffinity,
    std::map<std::string, std::string>&& envi,
    const std::function<Callback> completed) :
    jobId(jobId), taskId(taskId), requeueCount(requeueCount), taskExecutionId(String::Join("_", taskId, requeueCount)),
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

    Logger::Debug(this->jobId, this->taskId, this->requeueCount, "Created thread {0}", this->threadId);

    return pplx::task<pid_t>(this->started);
}

void Process::Kill(int forcedExitCode)
{
    if (forcedExitCode != 0x0FFFFFFF)
    {
        this->SetExitCode(forcedExitCode);
    }

    if (this->processId != 0)
    {
        this->ExecuteCommand("/bin/bash", "EndTask.sh", this->taskExecutionId, this->processId);
    }

    this->GetStatisticsFromCGroup();
}

void Process::GetStatisticsFromCGroup()
{
    // TODO: protect this section.
}

void Process::OnCompleted()
{
    try
    {
        this->callback(this->exitCode, this->message.str(), this->userTime, this->kernelTime);
    }
    catch (const std::exception& ex)
    {
        Logger::Error(this->jobId, this->taskId, this->requeueCount, "Exception happened when callback, ex = {0}", ex.what());
    }
    catch (...)
    {
        Logger::Error(this->jobId, this->taskId, this->requeueCount, "Unknown exception happened when callback");
    }
}

void* Process::ForkThread(void* arg)
{
    Process* const p = static_cast<Process* const>(arg);
    std::string path;

    int ret = p->CreateTaskFolder();
    if (ret != 0)
    {
        p->message << "Task " << p->taskId << ": error when create task folder, ret " << ret << std::endl;
        Logger::Error(p->jobId, p->taskId, p->requeueCount, "error when create task folder, ret {0}", ret);

        // TODO fetch the errno.
        p->SetExitCode(ret);
        goto Final;
    }

    path = p->BuildScript();

    if (path.empty())
    {
        p->message << "Error when build script." << std::endl;
        Logger::Error(p->jobId, p->taskId, p->requeueCount, "Error when build script.");

        // TODO fetch the errno.
        p->SetExitCode(-1);
        goto Final;
    }

    if (!p->ExecuteCommand("/bin/bash", "PrepareTask.sh", p->taskExecutionId, p->GetAffinity()))
    {
        goto Final;
    }

    p->processId = fork();

    if (p->processId < 0)
    {
        std::string errorMessage =
            errno == EAGAIN ? "number of process reached upper limit" : "not enough memory";

        p->message << "Failed to fork(), pid = " << p->processId << ", errno = " << errno
            << ", msg = " << errorMessage << std::endl;
        Logger::Error(p->jobId, p->taskId, p->requeueCount, "Failed to fork(), pid = {0}, errno = {1}, msg = {2}", p->processId, errno, errorMessage);

        p->SetExitCode(errno);
        goto Final;
    }

    if (p->processId == 0)
    {
        p->Run(path);
    }
    else
    {
        assert(p->processId > 0);
        p->started.set(p->processId);
        assert(p->processId > 0);
        p->Monitor();
    }

Final:
    p->ExecuteCommand("/bin/bash", "CleanupTask.sh", p->taskExecutionId, p->processId);
    p->ExecuteCommand("rm -rf", p->taskFolder);
    p->OnCompleted();

    pthread_exit(nullptr);
}

void Process::Monitor()
{
    assert(this->processId > 0);
    Logger::Debug(this->jobId, this->taskId, this->requeueCount, "Monitor the forked process {0}", this->processId);

    int status;
    rusage usage;
    assert(this->processId > 0);
    pid_t waitedPid = wait4(this->processId, &status, 0, &usage);
    assert(this->processId > 0);
    if (waitedPid == -1)
    {
        Logger::Error(this->jobId, this->taskId, this->requeueCount, "wait4 for process {0} error {1}", this->processId, errno);
        this->message << "wait4 for process " << this->processId << " error " << errno << std::endl;
        this->SetExitCode(errno);

        return;
    }

    assert(this->processId > 0);
    if (waitedPid != this->processId)
    {
        Logger::Error(this->jobId, this->taskId, this->requeueCount,
            "Process {0}: waited {1}, errno {2}", this->processId, waitedPid, errno);
        assert(false);

        int tmp;
        if (WIFEXITED(status)) Logger::Info(this->jobId, this->taskId, this->requeueCount, "Process {0}: WIFEXITED", this->processId);
        if ((tmp = WEXITSTATUS(status))) Logger::Info(this->jobId, this->taskId, this->requeueCount, "Process {0}: WEXITSTATUS: {1}", this->processId, tmp);
        if (WIFSIGNALED(status)) Logger::Info(this->jobId, this->taskId, this->requeueCount, "Process {0}: WIFSIGNALED", this->processId);
        if ((tmp = WTERMSIG(status))) Logger::Info(this->jobId, this->taskId, this->requeueCount, "Process {0}: WTERMSIG: {1}", this->processId, tmp);
        if (WCOREDUMP(status)) Logger::Info(this->jobId, this->taskId, this->requeueCount, "Process {0}: Core dumped.", this->processId);
        if (WIFSTOPPED(status)) Logger::Info(this->jobId, this->taskId, this->requeueCount, "Process {0}: WIFSTOPPED", this->processId);
        if (WSTOPSIG(status)) Logger::Info(this->jobId, this->taskId, this->requeueCount, "Process {0}: WSTOPSIG", this->processId);
        if (WIFCONTINUED(status)) Logger::Info(this->jobId, this->taskId, this->requeueCount, "Process {0}: WIFCONTINUED", this->processId);

        this->message << "Process " << this->processId << ": waited " << waitedPid << ", errno " << errno << std::endl;
        this->SetExitCode(errno);

        return;
    }

    if (WIFEXITED(status))
    {
        this->SetExitCode(WEXITSTATUS(status));

        std::string output;
        int ret = System::ExecuteCommand(output, "head -c 1500", this->stdOutFile);
        if (ret == 0)
        {
            this->message << "STDOUT: " << output << std::endl;
        }

        ret = System::ExecuteCommand(output, "head -c 1500", this->stdErrFile);
        if (ret == 0)
        {
            this->message << "STDERR: " << output << std::endl;
        }
    }
    else
    {
        Logger::Error(this->jobId, this->taskId, this->requeueCount, "wait4 for process {0} status {1}", this->processId, status);
        this->SetExitCode(-1);

        this->message << "wait4 for process " << this->processId << " status " << status << std::endl;
    }

    // TODO: use cgroup to report.
    this->userTime = usage.ru_utime;
    this->kernelTime = usage.ru_stime;

    Logger::Debug(this->jobId, this->taskId, this->requeueCount, "Process {0}: Monitor ended", this->processId);
    // TODO: Number of process;
    // TODO: ProcessIds
    // TODO: WorkingSet
}

void Process::Run(const std::string& path)
{
    std::vector<char> pathBuffer(path.cbegin(), path.cend());
    pathBuffer.push_back('\0');

    char* const args[] =
    {
        const_cast<char* const>("/bin/bash"),
        const_cast<char* const>("StartTask.sh"),
        const_cast<char* const>(this->taskExecutionId.c_str()),
        &pathBuffer[0],
        nullptr
    };

    auto envi = this->PrepareEnvironment();

    int ret = execvpe(args[0], args, const_cast<char* const*>(envi.get()));

    assert(ret == -1);

    std::cout << "Error occurred when execvpe, errno = " << errno << std::endl;

    exit(errno);
}

std::string Process::GetAffinity()
{
    return "0-3";
}

int Process::CreateTaskFolder()
{
    char folder[256];

    sprintf(folder, "/tmp/nodemanager_task_%d_%d.XXXXXX", this->taskId, this->requeueCount);

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
