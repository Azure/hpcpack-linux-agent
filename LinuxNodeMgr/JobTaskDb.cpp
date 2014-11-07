#include "JobTaskDb.h"
#include "RemotingExecutor.h"

void* MetricThread(void* arg)
{
	std::cout << "Start report thread." << std::endl;
	pthread_setcancelstate(PTHREAD_CANCEL_ENABLE, NULL);
	pthread_setcanceltype(PTHREAD_CANCEL_ASYNCHRONOUS, NULL);

	while (true)
	{
		std::string metricUri = JobTaskDb::GetInstance().GetMetricReportUri();

		if (!metricUri.empty())
		{
			HandleJson::Metric(metricUri);
		}

		sleep(Monitoring::Interval);
	}

	pthread_exit(0);
}

void* ReportingThread(void* arg)
{
	std::cout << "Start metric thread." << std::endl;
	pthread_setcancelstate(PTHREAD_CANCEL_ENABLE, NULL);
	pthread_setcanceltype(PTHREAD_CANCEL_ASYNCHRONOUS, NULL);

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
	// start monitoring service
	monitoring.Start();

    pthread_mutex_init(&jobTaskDbLock, NULL);

    pthread_create(&(this->threadId), NULL, ReportingThread, NULL);

	pthread_create(&(this->metricThreadId), NULL, MetricThread, NULL);
}

JobTaskDb::~JobTaskDb()
{
	if (executor != NULL)
		delete executor;

	// stop monitoring service
	monitoring.Stop();

	pthread_mutex_destroy(&jobTaskDbLock);

	if (threadId != 0){
		pthread_cancel(threadId);
		pthread_join(threadId, NULL);
	}

	if (metricThreadId != 0){
		pthread_cancel(metricThreadId);
		pthread_join(metricThreadId, NULL);
	}
}

void JobTaskDb::SetReportUri(const std::string& reportUri)
{
    pthread_mutex_lock(&jobTaskDbLock);
    this->reportUri = reportUri;
	this->metricReportUri = GetMetricReportUri(this->reportUri);

    std::ofstream uriFile("ReportUri", std::ios::trunc);
    uriFile << reportUri;

    std::cout << "ReportUri recorded: " << reportUri << std::endl;

    pthread_mutex_unlock(&jobTaskDbLock);
}

const std::string JobTaskDb::GetReportUri()
{
	return this->reportUri;
}

const std::string JobTaskDb::GetMetricReportUri()
{
	return this->metricReportUri;
}

std::string JobTaskDb::GetMetricReportUri(std::string & reportUri)
{
	size_t found = reportUri.find_last_of('/');
	if (found != std::string::npos){
		std::string metricReportUri = reportUri.substr(0, found + 1) + "metricreported";
		std::cout << "GetMetricReportUri : " << metricReportUri << std::endl;
		return metricReportUri;
	}
	return "";
}

JobTaskDb* JobTaskDb::instance = NULL;

json::value UMID::GetJson() const
{
	json::value obj;
	obj[U("MetricId")] = json::value(this->MetricId);
	obj[U("InstanceId")] = json::value(this->InstanceId);
	return obj;
}

json::value ComputeNodeMetricInformation::GetJson() const
{
	json::value obj;
	obj[U("Name")] = json::value::string(this->Name);
	obj[U("Time")] = json::value::string(this->Time);

	std::vector<json::value> arr1;
	std::vector<json::value> arr2;
	for (std::map<int, UMID*>::const_iterator it = this->Umids.begin();
		it != this->Umids.end();
		it++)
	{
		int no = it->first;
		arr1.push_back(it->second->GetJson());
		arr2.push_back(json::value::number(this->Values.at(no)));
	}

	obj[U("Umids")] = json::value::array(arr1);
	obj[U("Values")] = json::value::array(arr2);

	obj[U("TickCount")] = json::value::number(this->TickCount);
	return obj;
}

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

json::value JobTaskDb::GetMetricInfo()
{
	ComputeNodeMetricInformation cnmi(nodeInfo.Name);
	// add metric data
	UMID cpuUsage(1, 1);
	cnmi.Umids[0] = &cpuUsage;
	cnmi.Values[0] = monitoring.GetCpuUsage();

	UMID memoryUsage(3, 0);
	cnmi.Umids[1] = &memoryUsage;
	cnmi.Values[1] = monitoring.GetAvailableMemory();

	UMID networkUsage(12, 1);
	cnmi.Umids[2] = &networkUsage;
	cnmi.Values[2] = monitoring.GetNetworkUsage();

	json::value obj = cnmi.GetJson();
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
