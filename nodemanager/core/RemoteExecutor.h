#ifndef REMOTEEXECUTOR_H
#define REMOTEEXECUTOR_H

#include "IRemoteExecutor.h"
#include "JobTaskTable.h"
#include "Process.h"

namespace hpc
{
    namespace core
    {
        class RemoteExecutor : public IRemoteExecutor
        {
            public:
                RemoteExecutor();
                // TODO: delete all processes in destructor.
                ~RemoteExecutor() { }

                virtual bool StartJobAndTask(hpc::arguments::StartJobAndTaskArgs&& args, const std::string& callbackUri);
                virtual bool StartTask(hpc::arguments::StartTaskArgs&& args, const std::string& callbackUri);
                virtual bool EndJob(hpc::arguments::EndJobArgs&& args);
                virtual bool EndTask(hpc::arguments::EndTaskArgs&& args);
                virtual bool Ping(const std::string& callbackUri);
                virtual bool Metric(const std::string& callbackUri);

            protected:
            private:
                JobTaskTable jobTaskTable;

                // TODO: Make map hold Process directly.
                std::map<int, Process*> processes;
                pthread_rwlock_t lock;
        };
    }
}

#endif // REMOTEEXECUTOR_H
