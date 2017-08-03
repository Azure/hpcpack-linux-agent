#include <memory.h>
#include <sys/wait.h>
#include <sys/resource.h>
#include <fstream>
#include <cpprest/http_client.h>
#include <boost/algorithm/string/predicate.hpp>

#include "Process.h"
#include "../utils/Logger.h"
#include "../utils/String.h"
#include "../common/ErrorCodes.h"
#include "../utils/WriterLock.h"
#include "../data/OutputData.h"
#include "HttpHelper.h"

using namespace hpc::core;
using namespace hpc::utils;
using namespace hpc::common;
using namespace hpc::data;
using namespace http;

Process::Process(
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
    const std::function<Callback> completed) :
    jobId(jobId), taskId(taskId), requeueCount(requeueCount), taskExecutionId(String::Join("_", taskExecutionName, taskId, requeueCount)),
    commandLine(cmdLine), stdOutFile(standardOut), stdErrFile(standardErr), stdInFile(standardIn),
    workDirectory(workDir), userName(user.empty() ? "root" : user), dockerImage(envi["CCP_DOCKER_IMAGE"]), dumpStdout(dumpStdoutToExecutionMessage),
    affinity(cpuAffinity), environments(envi), callback(completed), processId(0)
{
    this->streamOutput = boost::algorithm::starts_with(stdOutFile, "http://") ||
        boost::algorithm::starts_with(stdOutFile, "https://");

    Logger::Debug(this->jobId, this->taskId, this->requeueCount, "{0}, stream ? {1}", stdOutFile, this->streamOutput);
}

Process::~Process()
{
    Logger::Debug(this->jobId, this->taskId, this->requeueCount, "~Process");
    this->Kill();

    pthread_rwlock_destroy(&this->lock);
}

void Process::Cleanup()
{
    std::string output;
    System::ExecuteCommandOut(output, "/bin/bash", "CleanupAllTasks.sh");
    Logger::Info("Cleanup zombie result: {0}", output);
}

pplx::task<std::pair<pid_t, pthread_t>> Process::Start(std::shared_ptr<Process> self)
{
    this->SetSelfPtr(self);
    pthread_create(&this->threadId, nullptr, ForkThread, this);

    Logger::Debug(this->jobId, this->taskId, this->requeueCount, "Created thread {0}", this->threadId);

    return pplx::task<std::pair<pid_t, pthread_t>>(this->started);
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
        this->ExecuteCommand("/bin/bash", "EndTask.sh", this->taskExecutionId, this->processId, forced ? "1" : "0", this->taskFolder);
    }
}

const ProcessStatistics& Process::GetStatisticsFromCGroup()
{
    std::string stat;
    System::ExecuteCommandOut(stat, "/bin/bash", "Statistics.sh", this->taskExecutionId, this->taskFolder);

    Logger::Debug(this->jobId, this->taskId, this->requeueCount, "Statistics: {0}", stat);

    std::istringstream statIn(stat);

    WriterLock writerLock(&this->lock);
    statIn >> this->statistics.UserTimeMs;
    this->statistics.UserTimeMs *= 10;

    statIn >> this->statistics.KernelTimeMs;
    this->statistics.KernelTimeMs *= 10;

    statIn >> this->statistics.WorkingSetKb;
    Logger::Debug(this->jobId, this->taskId, this->requeueCount, "WorkingSet {0}", this->statistics.WorkingSetKb);
    this->statistics.WorkingSetKb /= 1024;

    this->statistics.ProcessIds.clear();

    int id;
    while (statIn >> id)
    {
        this->statistics.ProcessIds.push_back(id);
    }

    return this->statistics;
}

pplx::task<void> Process::OnCompleted()
{
    return pplx::task<void>(this->completed);
}

void Process::OnCompletedInternal()
{
    try
    {
        this->completed.set();

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

Start:
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

    if (!p->dockerImage.empty())
    {
        std::string envs;
        for (auto it : p->environments)
        {
            envs.append(it.first).append("=").append(it.second).append("\n");
        }        

        std::string envFile = p->taskFolder + "/environments"; 
        if (0 != System::WriteStringToFile(envFile, envs))
        {
            Logger::Error(p->jobId, p->taskId, p->requeueCount, "Failed to create environment file for docker task.");
            goto Final;
        }
    }

    if (0 != p->ExecuteCommand("/bin/bash", "PrepareTask.sh", p->taskExecutionId, p->GetAffinity(), p->taskFolder, p->userName))
    {
        goto Final;
    }

    if (-1 == pipe(p->stdoutPipe))
    {
        p->message << "Error when create stdout pipe." << std::endl;
        Logger::Error(p->jobId, p->taskId, p->requeueCount, "Error when create stdout pipe.");

        p->SetExitCode(errno);
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
        p->started.set(std::pair<pid_t, pthread_t>(p->processId, p->threadId));
        goto Final;
    }

    if (p->processId == 0)
    {
        p->Run(path);
    }
    else
    {
        assert(p->processId > 0);
        p->started.set(std::pair<pid_t, pthread_t>(p->processId, p->threadId));
        p->Monitor();
    }

Final:
    p->ExecuteCommandNoCapture("/bin/bash", "EndTask.sh", p->taskExecutionId, p->processId, "1", p->taskFolder);
    p->GetStatisticsFromCGroup();

    ret = p->ExecuteCommandNoCapture("/bin/bash", "CleanupTask.sh", p->taskExecutionId, p->processId, p->taskFolder);

    // Only clean up the folder when success.
    if (p->exitCode == 0)
    {
        p->ExecuteCommandNoCapture("rm -rf", p->taskFolder);
    }

    if (p->outputThreadId != 0)
    {
        int joinret = pthread_join(p->outputThreadId, nullptr);
        if (joinret != 0)
        {
            Logger::Error(p->jobId, p->taskId, p->requeueCount, "Join the output thread id {0}, ret = {1}", p->outputThreadId, joinret);
        }

        p->outputThreadId = 0;
    }

    // TODO: Add logic to precisely define 253 error.
    if ((p->exitCode == 82 && ret == 96) || p->exitCode == 253)
    {
        p->exitCodeSet = false;
        p->exitCode = (int)hpc::common::ErrorCodes::DefaultExitCode;
        Logger::Error(p->jobId, p->taskId, p->requeueCount, "Exit Code {0} Reset exit code and retry to fork()", p->exitCode);
        goto Start;
    }

    p->ended = true;

    auto tmp = p->stdErr.str();
    if (!tmp.empty()) { p->message << tmp; }

    p->OnCompletedInternal();

    p->ResetSelfPtr();

    pthread_detach(pthread_self());
    pthread_exit(nullptr);
}

void* Process::ReadPipeThread(void* p)
{
    Logger::Info("Started reading pipe thread");
    auto* process = static_cast<Process*>(p);

    const std::string& uri = process->stdOutFile;
    close(process->stdoutPipe[1]);

    int bytesRead = 0;
    char buffer[1024];
    int order = 0;
    while ((bytesRead = read(process->stdoutPipe[0], buffer, sizeof(buffer) - 1)) > 0)
    {
        buffer[bytesRead] = '\0';
        auto readStr = String::Join("", buffer);

        Logger::Debug("read {0} bytes, streamOutput {1}", bytesRead, process->streamOutput);
        if (process->streamOutput)
        {
            // send out readStr
            process->SendbackOutput(uri, readStr, order++);
        }
        else
        {
            process->stdErr << buffer;
        }
    }

    Logger::Debug("read end. treamOutput {0}", process->streamOutput);
    if (process->streamOutput)
    {
        process->SendbackOutput(uri, std::string(), order++);
    }

    close(process->stdoutPipe[0]);

    pthread_exit(nullptr);
}

void Process::SendbackOutput(const std::string& uri, const std::string& output, int order) const
{
    try
    {
        OutputData od(System::GetNodeName(), order, output);

        if (output.empty()) { od.Eof = true; }

        auto jsonBody = od.ToJson();
        if (!jsonBody.is_null())
        {
            Logger::Debug(this->jobId, this->taskId, this->requeueCount,
                "Callback to {0} with {1}", uri, jsonBody);

            auto client = HttpHelper::GetHttpClient(uri);
            auto request = HttpHelper::GetHttpRequest(methods::POST, jsonBody);
            http_response response = client->request(*request).get();

            Logger::Info(this->jobId, this->taskId, this->requeueCount,
                "Callback to {0} response code {1}", uri, response.status_code());
        }
    }
    catch (const std::exception& ex)
    {
        Logger::Error(this->jobId, this->taskId, this->requeueCount,
            "Exception when sending back output. {0}", ex.what());
    }
}

void Process::Monitor()
{
    assert(this->processId > 0);
    Logger::Debug(this->jobId, this->taskId, this->requeueCount, "Monitor the forked process {0}", this->processId);

    pthread_create(&this->outputThreadId, nullptr, Process::ReadPipeThread, this);

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
            "Process {0}: exit code {1}", this->processId, WEXITSTATUS(status));
        this->SetExitCode(WEXITSTATUS(status));

        if (!this->streamOutput)
        {
            std::string output;

            int ret = 0;
            if (this->dumpStdout)
            {
                ret = System::ExecuteCommandOut(output, "head -c 1500", this->stdOutFile);
                if (ret == 0)
                {
                    this->message << "STDOUT: " << output << std::endl;
                }
            }

            if (this->stdOutFile != this->stdErrFile)
            {
                ret = System::ExecuteCommandOut(output, "head -c 1500", this->stdErrFile);
                if (ret == 0)
                {
                    this->message << "STDERR: " << output << std::endl;
                }
            }
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

    Logger::Debug(this->jobId, this->taskId, this->requeueCount, "Process {0}: Monitor ended", this->processId);
}

void Process::Run(const std::string& path)
{
    if (this->streamOutput)
    {
        // in clusrun case, only monitor stdout, because stderr will be redirected
        // to stdout
        dup2(this->stdoutPipe[1], 1);
    }
    else
    {
        // in normal case, only monitor error messages from our own script
        // customer script output will be redirected to the stdout/stderr file directly.
        dup2(this->stdoutPipe[1], 2);
    }

    close(this->stdoutPipe[0]);
    close(this->stdoutPipe[1]);

    std::vector<char> pathBuffer(path.cbegin(), path.cend());
    pathBuffer.push_back('\0');

    char* const args[] =
    {
        const_cast<char* const>("/bin/bash"),
        const_cast<char* const>("StartTask.sh"),
        const_cast<char* const>(this->taskExecutionId.c_str()),
        &pathBuffer[0],
        const_cast<char* const>(this->userName.c_str()),
        const_cast<char* const>(this->taskFolder.c_str()),
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

    int ret = System::CreateTempFolder(folder, this->userName);

    if (ret != 0)
    {
        Logger::Debug(this->jobId, this->taskId, this->requeueCount, "CreateTaskFolder failed exitCode {0}.", ret);
    }

    this->taskFolder = folder;

    return ret;
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

    Logger::Debug("{0}, {1}", this->taskFolder, this->workDirectory);
    if (this->workDirectory.empty())
    {
        fs << this->taskFolder;
    }
    else
    {
        fs << this->workDirectory;
    }

    fs << " || exit $?";
    fs << std::endl << std::endl;

    if (this->stdOutFile.empty()) this->stdOutFile = this->taskFolder + "/stdout.txt";
    if (this->stdErrFile.empty()) this->stdErrFile = this->taskFolder + "/stderr.txt";

    // before
    fs << "echo before >" << this->taskFolder << "/before1.txt 2>" << this->taskFolder << "/before2.txt";
    fs << " || ([ \"$?\" = \"1\" ] && exit 253)" << std::endl;

    // test
    fs << "echo test >" << this->taskFolder << "/stdout.txt 2>" << this->taskFolder << "/stderr.txt";
    fs << " || ([ \"$?\" = \"1\" ] && exit 253)" << std::endl << std::endl;

    // run
    if (this->streamOutput)
    {
        fs << "/bin/bash " << cmd << " 2>&1";
    }
    else if (this->stdOutFile == this->stdErrFile)
    {
        fs << "/bin/bash " << cmd << " >" << this->stdOutFile << " 2>&1";
    }
    else
    {
        fs << "/bin/bash " << cmd << " >" << this->stdOutFile << " 2>" << this->stdErrFile;
    }

    if (!this->stdInFile.empty())
    {
        fs << " <" << this->stdInFile;
    }

    fs << std::endl;
    fs << "ec=$?" << std::endl;
    fs << "[ $ec -ne 0 ] && exit $ec" << std::endl;

    fs << std::endl << std::endl;

    // after
    fs << "echo after >" << this->taskFolder << "/after1.txt 2>" << this->taskFolder << "/after2.txt";
    fs << " || ([ \"$?\" = \"1\" ] && exit 253)" << std::endl;

    fs.close();

    if (!this->dockerImage.empty())
    {
        return std::move(runDirInOut);
    }

    std::string runUser = this->taskFolder + "/run_user.sh";
    std::ofstream fsRunUser(runUser, std::ios::trunc);
    fsRunUser << "#!/bin/bash" << std::endl << std::endl;

    if (!this->userName.empty())
    {
        fsRunUser << "sudo -H -E -u " << this->userName << " env \"PATH=$PATH\" ";
    }

    fsRunUser << "/bin/bash " << runDirInOut << std::endl;

    fsRunUser.close();

    return std::move(runUser);
}

std::unique_ptr<const char* []> Process::PrepareEnvironment()
{
    this->environmentsBuffer.clear();

    auto it = this->environments.find(std::string("PATH"));

    if (it == this->environments.end())
    {
        char* currentPath = getenv("PATH");
        this->environmentsBuffer.push_back(std::string("PATH=") + currentPath);
    }

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
