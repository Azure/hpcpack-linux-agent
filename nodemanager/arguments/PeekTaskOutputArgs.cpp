#include "PeekTaskOutputArgs.h"
#include "../utils/JsonHelper.h"

using namespace hpc::arguments;
using namespace hpc::utils;

PeekTaskOutputArgs::PeekTaskOutputArgs(int jobId, int taskId)
    : JobId(jobId), TaskId(taskId)
{
    //ctor
}

PeekTaskOutputArgs PeekTaskOutputArgs::FromJson(const json::value& j)
{
    PeekTaskOutputArgs args(
        JsonHelper<int>::Read("JobId", j),
        JsonHelper<int>::Read("TaskId", j));

    return std::move(args);
}
