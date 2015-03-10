#ifndef REMOTEEXECUTOR_H
#define REMOTEEXECUTOR_H

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
                RemoteExecutor();
                // TODO: delete all processes in destructor.
                ~RemoteExecutor() { pthread_rwlock_destroy(&this->lock); }

                virtual bool StartJobAndTask(hpc::arguments::StartJobAndTaskArgs&& args, const std::string& callbackUri);
                virtual bool StartTask(hpc::arguments::StartTaskArgs&& args, const std::string& callbackUri);
                virtual bool EndJob(hpc::arguments::EndJobArgs&& args);
                virtual bool EndTask(hpc::arguments::EndTaskArgs&& args);
                virtual bool Ping(const std::string& callbackUri);
                virtual bool Metric(const std::string& callbackUri);

            protected:
            private:
                std::string LoadReportUri(const std::string& fileName);
                void SaveReportUri(const std::string& fileName, const std::string& uri);
                bool TerminateTask(int taskId);

                const int NodeInfoReportInterval = 30;
                const int MetricReportInterval = 2;
                const std::string NodeInfoUriFileName = "NodeInfoReportUri";
                const std::string MetricUriFileName = "MetricReportUri";

                JobTaskTable jobTaskTable;
                Monitor monitor;

                std::unique_ptr<Reporter> nodeInfoReporter;
                std::unique_ptr<Reporter> metricReporter;

                // TODO: Make map hold Process directly.
                std::map<int, std::shared_ptr<Process>> processes;
                pthread_rwlock_t lock;
        };
    }
}

#endif // REMOTEEXECUTOR_H
