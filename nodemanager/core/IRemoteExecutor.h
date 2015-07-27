#ifndef IREMOTEEXECUTOR_H
#define IREMOTEEXECUTOR_H

#include "../arguments/StartJobAndTaskArgs.h"
#include "../arguments/StartTaskArgs.h"
#include "../arguments/EndJobArgs.h"
#include "../arguments/EndTaskArgs.h"
#include "../arguments/MetricCountersConfig.h"

namespace hpc
{
    namespace core
    {
        class IRemoteExecutor
        {
            public:
                virtual web::json::value StartJobAndTask(hpc::arguments::StartJobAndTaskArgs&& args, const std::string& callbackUri) = 0;
                virtual web::json::value StartTask(hpc::arguments::StartTaskArgs&& args, const std::string& callbackUri) = 0;
                virtual web::json::value EndJob(hpc::arguments::EndJobArgs&& args) = 0;
                virtual web::json::value EndTask(hpc::arguments::EndTaskArgs&& args, const std::string& callbackUri) = 0;
                virtual web::json::value Ping(const std::string& callbackUri) = 0;
                virtual web::json::value Metric(const std::string& callbackUri) = 0;
                virtual web::json::value MetricConfig(hpc::arguments::MetricCountersConfig&& config, const std::string& callbackUri) = 0;
        };
    }
}

#endif // IREMOTEEXECUTOR_H
