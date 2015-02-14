#ifndef IREMOTEEXECUTOR_H
#define IREMOTEEXECUTOR_H

#include "arguments/StartJobAndTaskArgs.h"
#include "arguments/StartTaskArgs.h"
#include "arguments/EndJobArgs.h"
#include "arguments/EndTaskArgs.h"

class IRemoteExecutor
{
    public:
        virtual bool StartJobAndTask(const hpc::arguments::StartJobAndTaskArgs& args) = 0;
        virtual bool StartTask(const hpc::arguments::StartTaskArgs& args) = 0;
        virtual bool EndJob(const hpc::arguments::EndJobArgs& args) = 0;
        virtual bool EndTask(const hpc::arguments::EndTaskArgs& args) = 0;
        virtual bool Ping(const std::string& callbackUri) = 0;
        virtual bool Metric(const std::string& callbackUri) = 0;
};

#endif // IREMOTEEXECUTOR_H
