#ifndef JOBTASKDB_H
#define JOBTASKDB_H

#include "ProcessStartInfo.h"
#include "Executor.h"
#include "Monitoring.h"

#include <syslog.h>
#include <time.h>

class Executor;

struct UMID
{
public:
	UMID(unsigned short metricId, unsigned short instanceId)
		: MetricId(metricId), InstanceId(instanceId) {}
	web::json::value GetJson() const;
	unsigned short MetricId;
	unsigned short InstanceId;
};

struct ComputeNodeMetricInformation
{
public:
	ComputeNodeMetricInformation(std::string nodeName)
		: Name(nodeName)
	{
		time_t t;
		time(&t);
		Time = ctime(&t);
		TickCount = Monitoring::Interval;
	}
	web::json::value GetJson() const;
	std::string Name;
	std::string Time;
	std::map<int, UMID*> Umids;
	std::map<int, float> Values;
	int TickCount;
};

struct ComputeClusterTaskInformation
{
public:
    ComputeClusterTaskInformation(int jobId, int taskId, ProcessStartInfo* startInfo, const std::string& callbackUri)
        : TaskId(taskId), StartInfo(startInfo), CallbackUri(callbackUri), JobId(jobId) {}

    ~ComputeClusterTaskInformation() { delete StartInfo; }

    web::json::value GetEventArgJson() const;
    web::json::value GetJson() const;
    int ExitCode;
    bool Exited;
    long long KernelProcessorTime;
    std::string Message;
    int NumberOfProcesses;
    bool PrimaryTask;
    std::string ProcessIds;
    int TaskId;
    int TaskRequeueCount;
    long long UserProcessorTime;
    int WorkingSet;

    ProcessStartInfo *StartInfo;
    std::string CallbackUri;
    int JobId;
};

struct ComputeClusterJobInformation
{
public:
    ComputeClusterJobInformation(int jobId) : JobId(jobId) {}
    web::json::value GetJson() const;
    int JobId;
    std::map<int, ComputeClusterTaskInformation*> Tasks;
};

enum ComputeNodeAvailability
{
    AlwaysOn = 0,
    Available = 1,
    Occupied = 2
};

struct ComputeClusterNodeInformation
{
public:
    web::json::value GetJson() const;
    ComputeNodeAvailability Availability;
    bool JustStarted;
    std::string MacAddress;
    std::string Name;

    std::map<int, ComputeClusterJobInformation*> Jobs;
};

class JobTaskDb
{
    public:
        static JobTaskDb& GetInstance()
        {
            return *instance;
        }

        // Not acquiring locks for singleton. We can initialize in the very beginning.
        static void Initialize()
        {
			std::cout << "JobTaskDb : Initializing" << std::endl;
            instance = new JobTaskDb();
        
            char nodeName[256];
            if (-1 == gethostname(nodeName, 255))
            {
                syslog(LOG_INFO, "gethostname failed!");
                exit(-1);
            }

            std::cout << nodeName << std::endl;

            instance->nodeInfo.Name = nodeName;
            instance->nodeInfo.JustStarted = true;

            std::cout << "Loading Report URI" << std::endl;
            std::ifstream uriFile("ReportUri", std::ios::in);
            if (uriFile)
            {
                uriFile >> instance->reportUri;
				instance->metricReportUri = instance->GetMetricReportUri(instance->reportUri);

                std::cout << "Loaded Report Uri: " << instance->reportUri << std::endl;            
            }
            else
            {
                std::cout << "Cannot load Report Uri." << std::endl;
            }
        }

        web::json::value GetNodeInfo();
		web::json::value GetMetricInfo();
        web::json::value GetTaskInfo(int jobId, int taskId) const;
        void StartJobAndTask(int jobId, int taskId, ProcessStartInfo* startInfo, const std::string& callbackUri);
        void EndJob(int jobId);
        void EndTask(int jobId, int taskId);
        void RemoveTask(int jobId, int taskId);
        void SetReportUri(const std::string& reportUri);
        const std::string GetReportUri();
		const std::string GetMetricReportUri();
		std::string GetMetricReportUri(std::string & reportUri);

    protected:
    private:
		JobTaskDb();
		~JobTaskDb();

        void Callback(std::string callbackUri, web::json::value callbackBody);

        static JobTaskDb* instance;
        pthread_mutex_t jobTaskDbLock;
        pthread_t threadId;
		pthread_t metricThreadId;
        std::string reportUri;
		std::string metricReportUri;

        ComputeClusterNodeInformation nodeInfo;
        Executor* executor;
		Monitoring monitoring;
};

#endif // JOBTASKDB_H
