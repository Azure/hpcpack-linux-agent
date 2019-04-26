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
                ~RemoteExecutor()
                {
                    Logger::Info("Closing the Remote Executor.");
                    this->cts.cancel();
                    pthread_rwlock_destroy(&this->lock);
                    Logger::Info("Closed the Remote Executor.");
                }

                virtual pplx::task<web::json::value> StartJobAndTask(hpc::arguments::StartJobAndTaskArgs&& args, std::string&& callbackUri);
                virtual pplx::task<web::json::value> StartTask(hpc::arguments::StartTaskArgs&& args, std::string&& callbackUri);
                virtual pplx::task<web::json::value> EndJob(hpc::arguments::EndJobArgs&& args);
                virtual pplx::task<web::json::value> EndTask(hpc::arguments::EndTaskArgs&& args, std::string&& callbackUri);
                virtual pplx::task<web::json::value> Ping(std::string&& callbackUri);
                virtual pplx::task<web::json::value> Metric(std::string&& callbackUri);
                virtual pplx::task<web::json::value> MetricConfig(hpc::arguments::MetricCountersConfig&& config, std::string&& callbackUri);
                virtual pplx::task<web::json::value> PeekTaskOutput(hpc::arguments::PeekTaskOutputArgs&& args);

            protected:
            private:
                static void* GracePeriodElapsed(void* data);

                void StartHeartbeat();
                void UpdateStatistics();
                void StartMetric();
                void StartHostsManager();

                void ResyncAndInvalidateCache();

                const hpc::data::ProcessStatistics* TerminateTask(
                    int jobId, int taskId, int requeueCount,
                    uint64_t processKey, int exitCode, bool forced, bool mpiDockerTask);

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

                pplx::cancellation_token_source cts;
        };
    }
}

#endif // REMOTEEXECUTOR_H
