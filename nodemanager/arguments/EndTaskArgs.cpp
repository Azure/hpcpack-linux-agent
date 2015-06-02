#include "EndTaskArgs.h"
#include "../utils/JsonHelper.h"

using namespace hpc::arguments;
using namespace hpc::utils;

EndTaskArgs::EndTaskArgs(int jobId, int taskId, int gracePeriodSeconds)
    : JobId(jobId), TaskId(taskId), TaskCancelGracePeriodSeconds(gracePeriodSeconds)
{
    //ctor
}

EndTaskArgs EndTaskArgs::FromJson(const json::value& j)
{
    EndTaskArgs args(
        JsonHelper<int>::Read("JobId", j),
        JsonHelper<int>::Read("TaskId", j),
        JsonHelper<int>::Read("TaskCancelGracePeriod", j));

    return std::move(args);
}
