#include "JobTaskDb.h"
#include "RemotingExecutor.h"

void* ReportingThread(void* arg)
{
    while (true)
    {
        std::string reportUri = JobTaskDb::GetInstance().GetReportUri();

        if (!reportUri.empty())
        {
            HandleJson::Ping(reportUri);
        }

        sleep(30);
    }

    pthread_exit(0);
}

JobTaskDb::JobTaskDb() : executor(new Executor())
{
    pthread_mutex_init(&jobTaskDbLock, NULL);

    this->threadId = new pthread_t();
    pthread_create(this->threadId, NULL, ReportingThread, NULL);
}

void JobTaskDb::SetReportUri(const std::string& reportUri)
{
    pthread_mutex_lock(&jobTaskDbLock);
    this->reportUri = reportUri;

    std::ofstream uriFile("ReportUri", std::ios::trunc);
    uriFile << reportUri;

    std::cout << "ReportUri recorded: " << reportUri << std::endl;

    pthread_mutex_unlock(&jobTaskDbLock);
}

const std::string JobTaskDb::GetReportUri()
{
    pthread_mutex_lock(&jobTaskDbLock);
    std::string reportUri = this->reportUri;
    pthread_mutex_unlock(&jobTaskDbLock);

    return reportUri;
}

JobTaskDb* JobTaskDb::instance = NULL;

json::value ComputeClusterTaskInformation::GetJson() const
{
    json::value obj;
    obj[U("ExitCode")] = json::value::number(this->ExitCode);
    obj[U("Exited")] = json::value::boolean(this->Exited);
    obj[U("KernelProcessorTime")] = json::value((int64_t)this->KernelProcessorTime);
    obj[U("NumberOfProcesses")] = json::value::number(this->NumberOfProcesses);
    obj[U("PrimaryTask")] = json::value::boolean(this->PrimaryTask);
    obj[U("ProcessIds")] = json::value::string(this->ProcessIds);
    obj[U("TaskId")] = json::value::number(this->TaskId);
    obj[U("TaskRequeueCount")] = json::value::number(this->TaskRequeueCount);
    obj[U("UserProcessorTime")] = json::value((int64_t)this->UserProcessorTime);
    obj[U("WorkingSet")] = json::value::number(this->WorkingSet);
    obj[U("Message")] = json::value::string(this->Message);
    return obj;
}

json::value ComputeClusterTaskInformation::GetEventArgJson() const
{
    json::value obj;
    obj[U("JobId")] = json::value::number(this->JobId);
    obj[U("TaskInfo")] = this->GetJson();
    return obj;
}

json::value ComputeClusterJobInformation::GetJson() const
{
    json::value obj;
    obj[U("JobId")] = json::value::number(this->JobId);
    std::vector<json::value> ans;

    for (std::map<int, ComputeClusterTaskInformation*>::const_iterator it = this->Tasks.begin();
        it != this->Tasks.end();
        it ++)
    {
        ans.push_back(it->second->GetJson());
    }

    obj[U("Tasks")] = json::value::array(ans);
    return obj;
}

json::value ComputeClusterNodeInformation::GetJson() const
{
    json::value obj;
    obj[U("Availability")] = json::value::number(this->Availability);
    obj[U("JustStarted")] = json::value::boolean(this->JustStarted);
    obj[U("MacAddress")] = json::value::string(this->MacAddress);
    obj[U("Name")] = json::value::string(this->Name);
    std::vector<json::value> ans;

    for (std::map<int, ComputeClusterJobInformation*>::const_iterator it = this->Jobs.begin();
        it != this->Jobs.end();
        it ++)
    {
        ans.push_back(it->second->GetJson());
    }

    obj[U("Jobs")] = json::value::array(ans);
    return obj;
}

json::value JobTaskDb::GetNodeInfo()
{
    pthread_mutex_lock(&jobTaskDbLock);
    json::value obj = this->nodeInfo.GetJson();
    this->nodeInfo.JustStarted = false;
    pthread_mutex_unlock(&jobTaskDbLock);

    return obj;
}

void JobTaskDb::StartJobAndTask(int jobId, int taskId, ProcessStartInfo* startInfo, const std::string& callbackUri)
{
    std::cout << "Enter StartJobAndTask" << std::endl;

    pthread_mutex_lock(&jobTaskDbLock);

    std::cout << "GetLock" << std::endl;
    ComputeClusterJobInformation* jobInfo = NULL;

    std::map<int, ComputeClusterJobInformation*>::iterator it = this->nodeInfo.Jobs.find(jobId);

    std::cout << "JobInfoFound" << std::endl;
    if (it == this->nodeInfo.Jobs.end())
    {
        jobInfo = new ComputeClusterJobInformation(jobId);
        this->nodeInfo.Jobs[jobId] = jobInfo;
        std::cout << "Job added" << std::endl;
    }
    else
    {
        jobInfo = it->second;
    }

    ComputeClusterTaskInformation* taskInfo;
    std::map<int, ComputeClusterTaskInformation*>::iterator taskIt = jobInfo->Tasks.find(taskId);
    std::cout << "TaskInfoFound" << std::endl;

    if (taskIt == jobInfo->Tasks.end())
    {
        std::cout << "Before EXE startTask" << std::endl;
        taskInfo = this->executor->StartTask(jobId, taskId, startInfo, callbackUri);
        std::cout << "After Exe StartTask" << std::endl;
        jobInfo->Tasks[taskId] = taskInfo;
    }
    else
    {
        taskInfo = taskIt->second;
    }

    pthread_mutex_unlock(&jobTaskDbLock);
}

void JobTaskDb::EndJob(int jobId)
{
    pthread_mutex_lock(&jobTaskDbLock);

    ComputeClusterJobInformation* jobInfo = NULL;
    std::map<int, ComputeClusterJobInformation*>::iterator it = this->nodeInfo.Jobs.find(jobId);

    if (it != this->nodeInfo.Jobs.end())
    {
        jobInfo = it->second;

        for (std::map<int, ComputeClusterTaskInformation*>::iterator taskIt = jobInfo->Tasks.begin(); taskIt != jobInfo->Tasks.end(); taskIt++)
        {
            this->executor->EndTask(taskIt->second);
        }

        delete jobInfo;

        this->nodeInfo.Jobs.erase(jobId);
    }

    pthread_mutex_unlock(&jobTaskDbLock);
}

void JobTaskDb::EndTask(int jobId, int taskId)
{
    pthread_mutex_lock(&jobTaskDbLock);

    ComputeClusterJobInformation* jobInfo = NULL;

    std::map<int, ComputeClusterJobInformation*>::iterator it = this->nodeInfo.Jobs.find(jobId);
    if (it != this->nodeInfo.Jobs.end())
    {
        jobInfo = it->second;

        std::map<int, ComputeClusterTaskInformation*>::iterator taskIt = jobInfo->Tasks.find(taskId);
        if (taskIt != jobInfo->Tasks.end())
        {
            this->executor->EndTask(taskIt->second);
        }
    }

    pthread_mutex_unlock(&jobTaskDbLock);
}

void JobTaskDb::RemoveTask(int jobId, int taskId)
{
    pthread_mutex_lock(&jobTaskDbLock);

    ComputeClusterJobInformation* jobInfo = NULL;

    std::map<int, ComputeClusterJobInformation*>::iterator it = this->nodeInfo.Jobs.find(jobId);
    if (it != this->nodeInfo.Jobs.end())
    {
        jobInfo = it->second;

        std::map<int, ComputeClusterTaskInformation*>::iterator taskIt = jobInfo->Tasks.find(taskId);
        if (taskIt != jobInfo->Tasks.end())
        {
            jobInfo->Tasks.erase(taskId);
        }
    }

    pthread_mutex_unlock(&jobTaskDbLock);
}
