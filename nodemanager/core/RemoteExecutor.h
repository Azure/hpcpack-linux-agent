#ifndef REMOTEEXECUTOR_H
#define REMOTEEXECUTOR_H

#include <set>
#include <map>

#include "IRemoteExecutor.h"
#include "JobTaskTable.h"
#include "Monitor.h"
#include "Process.h"
#include "Reporter.h"
#include "HostsManager.h"
#include "../arguments/MetricCountersConfig.h"
#include "../data/ProcessStatistics.h"

namespace hpc
{
    namespace core
    {
        class RemoteExecutor : public IRemoteExecutor
        {
            public:
                RemoteExecutor(const std::string& networkName);
                ~RemoteExecutor() { pthread_rwlock_destroy(&this->lock); }

                virtual web::json::value StartJobAndTask(hpc::arguments::StartJobAndTaskArgs&& args, const std::string& callbackUri);
                virtual web::json::value StartTask(hpc::arguments::StartTaskArgs&& args, const std::string& callbackUri);
                virtual web::json::value EndJob(hpc::arguments::EndJobArgs&& args);
                virtual web::json::value EndTask(hpc::arguments::EndTaskArgs&& args, const std::string& callbackUri);
                virtual web::json::value Ping(const std::string& callbackUri);
                virtual web::json::value Metric(const std::string& callbackUri);
                virtual web::json::value MetricConfig(hpc::arguments::MetricCountersConfig&& config, const std::string& callbackUri);
            protected:
            private:
                static void* GracePeriodElapsed(void* data);

                void StartHeartbeat(const std::string& callbackUri);
                void StartMetric(const std::string& callbackUri);
                void StartHostsManager();

                const hpc::data::ProcessStatistics* TerminateTask(
                    int jobId, int taskId, int requeueCount,
                    uint64_t processKey, int exitCode, bool forced);

                void ReportTaskCompletion(int jobId, int taskId, int taskRequeueCount, json::value jsonBody, const std::string& callbackUri);

                const int UnknowId = 999;
                const int NodeInfoReportInterval = 30;
                const int MetricReportInterval = 1;
                const int RegisterInterval = 300;
                const int DefaultHostsFetchInterval = 300;
                const int MinHostsFetchInterval = 30;

                JobTaskTable jobTaskTable;
                Monitor monitor;

                std::unique_ptr<Reporter<json::value>> nodeInfoReporter;
                std::unique_ptr<Reporter<json::value>> registerReporter;
                std::unique_ptr<Reporter<std::vector<unsigned char>>> metricReporter;
                std::unique_ptr<HostsManager> hostsManager;

                std::map<uint64_t, std::shared_ptr<Process>> processes;
                std::map<int, std::tuple<std::string, bool, bool, bool, bool, std::string>> jobUsers;
                std::map<std::string, std::set<int>> userJobs;
                pthread_rwlock_t lock;
        };
    }
}

#endif // REMOTEEXECUTOR_H
