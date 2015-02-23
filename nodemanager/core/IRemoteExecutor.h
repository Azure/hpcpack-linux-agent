#ifndef IREMOTEEXECUTOR_H
#define IREMOTEEXECUTOR_H

#include "../arguments/StartJobAndTaskArgs.h"
#include "../arguments/StartTaskArgs.h"
#include "../arguments/EndJobArgs.h"
#include "../arguments/EndTaskArgs.h"

namespace hpc
{
    namespace core
    {
        class IRemoteExecutor
        {
            public:
                virtual bool StartJobAndTask(hpc::arguments::StartJobAndTaskArgs&& args, const std::string& callbackUri) = 0;
                virtual bool StartTask(hpc::arguments::StartTaskArgs&& args, const std::string& callbackUri) = 0;
                virtual bool EndJob(hpc::arguments::EndJobArgs&& args) = 0;
                virtual bool EndTask(hpc::arguments::EndTaskArgs&& args) = 0;
                virtual bool Ping(const std::string& callbackUri) = 0;
                virtual bool Metric(const std::string& callbackUri) = 0;
        };
    }
}

#endif // IREMOTEEXECUTOR_H
