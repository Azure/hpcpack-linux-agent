#include <memory.h>
#include <sys/wait.h>
#include <sys/resource.h>
#include <fstream>

#include "Process.h"
#include "../utils/Logger.h"
#include "../utils/String.h"
#include "../common/ErrorCodes.h"
#include "../utils/WriterLock.h"

using namespace hpc::core;
using namespace hpc::utils;
using namespace hpc::common;
using namespace hpc::data;

Process::Process(
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
    const std::function<Callback> completed) :
    jobId(jobId), taskId(taskId), requeueCount(requeueCount), taskExecutionId(String::Join("_", taskId, requeueCount)),
    commandLine(cmdLine), stdOutFile(standardOut), stdErrFile(standardErr), stdInFile(standardIn),
    workDirectory(workDir), userName(user.empty() ? "root" : user),
    affinity(cpuAffinity), environments(envi), callback(completed), processId(0)
{

}

Process::~Process()
{
    this->Kill();
    pthread_rwlock_destroy(&this->lock);
}

void Process::Cleanup()
{
    std::string output;
    System::ExecuteCommandOut(output, "/bin/bash", "CleanupAllTasks.sh");
    Logger::Info("Cleanup zombie result: {0}", output);
}

pplx::task<pid_t> Process::Start()
{
    pthread_create(&this->threadId, nullptr, ForkThread, this);

    Logger::Debug(this->jobId, this->taskId, this->requeueCount, "Created thread {0}", this->threadId);

    return pplx::task<pid_t>(this->started);
}

void Process::Kill(int forcedExitCode, bool forced)
{
    if (forcedExitCode != 0x0FFFFFFF)
    {
        Logger::Debug(this->jobId, this->taskId, this->requeueCount, "Setting forced ExitCode {0}", forcedExitCode);
        this->SetExitCode(forcedExitCode);
    }

    if (!this->ended)
    {
        this->ExecuteCommand("/bin/bash", "EndTask.sh", this->taskExecutionId, this->processId, forced ? "1" : "0");
    }
}

const ProcessStatistics& Process::GetStatisticsFromCGroup()
{
    std::string stat;
    System::ExecuteCommandOut(stat, "/bin/bash", "Statistics.sh", this->taskExecutionId);

    std::istringstream statIn(stat);

    WriterLock writerLock(&this->lock);
    statIn >> this->statistics.UserTimeMs;
    this->statistics.UserTimeMs *= 10;

    statIn >> this->statistics.KernelTimeMs;
    this->statistics.KernelTimeMs *= 10;

    statIn >> this->statistics.WorkingSetKb;
    this->statistics.WorkingSetKb /= 1024;

    this->statistics.ProcessIds.clear();

    int id;
    while (statIn >> id)
    {
        this->statistics.ProcessIds.push_back(id);
    }

    return this->statistics;
}

void Process::OnCompleted()
{
    try
    {
        this->callback(
            this->exitCode,
            this->message.str(),
            this->statistics);
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

        p->SetExitCode(ret);
        goto Final;
    }

    path = p->BuildScript();

    if (path.empty())
    {
        p->message << "Error when build script." << std::endl;
        Logger::Error(p->jobId, p->taskId, p->requeueCount, "Error when build script.");

        p->SetExitCode((int)ErrorCodes::BuildScriptError);
        goto Final;
    }

    if (0 != p->ExecuteCommand("/bin/bash", "PrepareTask.sh", p->taskExecutionId, p->GetAffinity()))
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
    p->ended = true;
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

        return;
    }

    if (WIFEXITED(status))
    {
        Logger::Info(this->jobId, this->taskId, this->requeueCount,
            "Process {0}: exite code {1}", this->processId, WEXITSTATUS(status));
        this->SetExitCode(WEXITSTATUS(status));

        std::string output;
        int ret = System::ExecuteCommandOut(output, "head -c 1500", this->stdOutFile);
        if (ret == 0)
        {
            this->message << "STDOUT: " << output << std::endl;
        }

        ret = System::ExecuteCommandOut(output, "head -c 1500", this->stdErrFile);
        if (ret == 0)
        {
            this->message << "STDERR: " << output << std::endl;
        }
    }
    else
    {
        if (WIFSIGNALED(status))
        {
            Logger::Info(this->jobId, this->taskId, this->requeueCount, "Process {0}: WIFSIGNALED Signal {1}", this->processId, WTERMSIG(status));
            this->message << "Process " << this->processId << ": WIFSIGNALED Signal " << WTERMSIG(status) << std::endl;
        }

        if (WCOREDUMP(status))
        {
            Logger::Info(this->jobId, this->taskId, this->requeueCount, "Process {0}: Core dumped.", this->processId);
            this->message << "Process " << this->processId << ": Core dumped." << std::endl;
        }

        Logger::Error(this->jobId, this->taskId, this->requeueCount, "Process {0}: wait4 status {1}", this->processId, status);
        this->SetExitCode(status);

        this->message << "Process " << this->processId << ": wait4 status " << status << std::endl;
    }

    this->GetStatisticsFromCGroup();

    Logger::Debug(this->jobId, this->taskId, this->requeueCount, "Process {0}: Monitor ended", this->processId);
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
        const_cast<char* const>(this->userName.c_str()),
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
    int cores, sockets;
    System::CPU(cores, sockets);

    std::string aff;
    if (!this->affinity.empty())
    {
        std::ostringstream result;
        for (int lastCore = 0, i = 0; i < cores; i++)
        {
            size_t affinityIndex = i / 64;
            int affinityOffset = i % 64;

            if (this->affinity.size() <= affinityIndex)
            {
                OutputAffinity(result, lastCore, i - 1);

                break;
            }

            uint64_t currentAffinity = this->affinity[affinityIndex];

            if (!((currentAffinity >> affinityOffset) << (63 - affinityOffset)))
            {
                // if the bit is not set;
                OutputAffinity(result, lastCore, i - 1);

                lastCore = i + 1;
            }
            else if (i == cores - 1)
            {
                OutputAffinity(result, lastCore, i);
            }
        }

        aff = result.str();
    }

    if (aff.size() > 1)
    {
        return aff.substr(1);
    }
    else
    {
        return String::Join("-", "0", cores - 1);
    }
}

int Process::CreateTaskFolder()
{
    char folder[256];

    sprintf(folder, "/tmp/nodemanager_task_%d_%d.XXXXXX", this->taskId, this->requeueCount);

    char* p = mkdtemp(folder);

    if (p)
    {
        this->taskFolder = p;

        int ret;
        ret = this->ExecuteCommand("chown -R", this->userName, this->taskFolder);

        if (ret == 0)
        {
            ret = this->ExecuteCommand("chmod -R u+rwX", this->taskFolder);
        }

        return ret;
    }
    else
    {
        return errno;
    }
}

std::string Process::BuildScript()
{
    std::string cmd = this->taskFolder + "/cmd.sh";
    std::ofstream fsCmd(cmd, std::ios::trunc);
    fsCmd << "#!/bin/bash" << std::endl << std::endl;
    fsCmd << this->commandLine << std::endl;
    fsCmd.close();

    std::string runDirInOut = this->taskFolder + "/run_dir_in_out.sh";

    std::ofstream fs(runDirInOut, std::ios::trunc);
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

    fs << " /bin/bash " << cmd
        << " >" << this->stdOutFile
        << " 2>" << this->stdErrFile;

    if (!this->stdInFile.empty())
    {
        fs << " <" << this->stdInFile;
    }

    fs << std::endl << std::endl;

    fs.close();

    std::string runUser = this->taskFolder + "/run_user.sh";
    std::ofstream fsRunUser(runUser, std::ios::trunc);
    fsRunUser << "#!/bin/bash" << std::endl << std::endl;

    if (!this->userName.empty())
    {
        fsRunUser << "sudo -E -u " << this->userName
            << " /bin/bash " << runDirInOut << std::endl;
    }
    else
    {
        fsRunUser << " /bin/bash " << runDirInOut << std::endl;
    }

    fsRunUser.close();

    return std::move(runUser);
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
