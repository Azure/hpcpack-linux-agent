#ifndef REMOTEEXECUTOR_H
#define REMOTEEXECUTOR_H

#include <set>
#include <map>

#include "IRemoteExecutor.h"
#include "JobTaskTable.h"
#include "Monitor.h"
#include "Process.h"
#include "Reporter.h"

namespace hpc
{
    namespace core
    {
        class RemoteExecutor : public IRemoteExecutor
        {
            public:
                RemoteExecutor(const std::string& networkName);
                // TODO: delete all processes in destructor.
                ~RemoteExecutor() { pthread_rwlock_destroy(&this->lock); }

                virtual web::json::value StartJobAndTask(hpc::arguments::StartJobAndTaskArgs&& args, const std::string& callbackUri);
                virtual web::json::value StartTask(hpc::arguments::StartTaskArgs&& args, const std::string& callbackUri);
                virtual web::json::value EndJob(hpc::arguments::EndJobArgs&& args);
                virtual web::json::value EndTask(hpc::arguments::EndTaskArgs&& args);
                virtual web::json::value Ping(const std::string& callbackUri);
                virtual web::json::value Metric(const std::string& callbackUri);

            protected:
            private:
                std::string LoadReportUri(const std::string& fileName);
                void SaveReportUri(const std::string& fileName, const std::string& uri);
                bool TerminateTask(int processKey, int exitCode);

                const int UnknowId = 999;
                const int NodeInfoReportInterval = 30;
                const int MetricReportInterval = 2;
                const int RegisterInterval = 300;
                const std::string NodeInfoUriFileName = "NodeInfoReportUri";
                const std::string MetricUriFileName = "MetricReportUri";
                const std::string RegisterUriFileName = "RegisterUri";

                JobTaskTable jobTaskTable;
                Monitor monitor;

                std::unique_ptr<Reporter<json::value>> nodeInfoReporter;
                std::unique_ptr<Reporter<json::value>> registerReporter;
                std::unique_ptr<Reporter<std::vector<unsigned char>>> metricReporter;

                std::map<uint64_t, std::shared_ptr<Process>> processes;
                std::map<int, std::tuple<std::string, bool, bool, bool, bool, std::string>> jobUsers;
                std::map<std::string, std::set<int>> userJobs;
                pthread_rwlock_t lock;
        };
    }
}

#endif // REMOTEEXECUTOR_H
