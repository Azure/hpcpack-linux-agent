#include "EndTaskArgs.h"
#include "../utils/JsonHelper.h"

using namespace hpc::arguments;
using namespace hpc::utils;

EndTaskArgs::EndTaskArgs(int jobId, int taskId) : JobId(jobId), TaskId(taskId)
{
    //ctor
}

EndTaskArgs&& EndTaskArgs::FromJson(const json::value& j)
{
    EndTaskArgs args(
        JsonHelper<int>::Read("JobId", j),
        JsonHelper<int>::Read("TaskId", j));

    return std::move(args);
}
