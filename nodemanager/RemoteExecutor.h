#ifndef REMOTEEXECUTOR_H
#define REMOTEEXECUTOR_H

#include "IRemoteExecutor.h"

namespace hpc
{
    class RemoteExecutor : public IRemoteExecutor
    {
        public:
            RemoteExecutor();

            virtual bool StartJobAndTask(const hpc::arguments::StartJobAndTaskArgs& args);
            virtual bool StartTask(const hpc::arguments::StartTaskArgs& args);
            virtual bool EndJob(const hpc::arguments::EndJobArgs& args);
            virtual bool EndTask(const hpc::arguments::EndTaskArgs& args);
            virtual bool Ping(const std::string& callbackUri);
            virtual bool Metric(const std::string& callbackUri);

        protected:
        private:
    };
}

#endif // REMOTEEXECUTOR_H
