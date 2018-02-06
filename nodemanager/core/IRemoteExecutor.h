#ifndef IREMOTEEXECUTOR_H
#define IREMOTEEXECUTOR_H

#include "../arguments/StartJobAndTaskArgs.h"
#include "../arguments/StartTaskArgs.h"
#include "../arguments/EndJobArgs.h"
#include "../arguments/EndTaskArgs.h"
#include "../arguments/MetricCountersConfig.h"
#include "../arguments/PeekTaskOutputArgs.h"

namespace hpc
{
    namespace core
    {
        class IRemoteExecutor
        {
            public:
                virtual pplx::task<web::json::value> StartJobAndTask(hpc::arguments::StartJobAndTaskArgs&& args, std::string&& callbackUri) = 0;
                virtual pplx::task<web::json::value> StartTask(hpc::arguments::StartTaskArgs&& args, std::string&& callbackUri) = 0;
                virtual pplx::task<web::json::value> EndJob(hpc::arguments::EndJobArgs&& args) = 0;
                virtual pplx::task<web::json::value> EndTask(hpc::arguments::EndTaskArgs&& args, std::string&& callbackUri) = 0;
                virtual pplx::task<web::json::value> Ping(std::string&& callbackUri) = 0;
                virtual pplx::task<web::json::value> Metric(std::string&& callbackUri) = 0;
                virtual pplx::task<web::json::value> MetricConfig(hpc::arguments::MetricCountersConfig&& config, std::string&& callbackUri) = 0;
                virtual pplx::task<web::json::value> PeekTaskOutput(hpc::arguments::PeekTaskOutputArgs&& args) = 0;
        };
    }
}

#endif // IREMOTEEXECUTOR_H
