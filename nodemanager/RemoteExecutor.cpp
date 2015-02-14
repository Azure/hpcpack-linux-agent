#include "RemoteExecutor.h"

using namespace hpc;

RemoteExecutor::RemoteExecutor()
{
    //ctor
}

bool RemoteExecutor::StartJobAndTask(const hpc::arguments::StartJobAndTaskArgs& args)
{
    return true;
}

bool RemoteExecutor::StartTask(const hpc::arguments::StartTaskArgs& args)
{
    return true;
}

bool RemoteExecutor::EndJob(const hpc::arguments::EndJobArgs& args)
{
    return true;
}

bool RemoteExecutor::EndTask(const hpc::arguments::EndTaskArgs& args)
{
    return true;
}

bool RemoteExecutor::Ping(const std::string& callbackUri)
{
    return true;
}

bool RemoteExecutor::Metric(const std::string& callbackUri)
{
    return true;
}
