#include "StartJobAndTaskArgs.h"
#include "../utils/JsonHelper.h"

using namespace hpc::arguments;
using namespace hpc::utils;

StartJobAndTaskArgs::StartJobAndTaskArgs(int jobId, int taskId, ProcessStartInfo&& startInfo) :
    JobId(jobId), TaskId(taskId), StartInfo(std::move(startInfo))
{
    //ctor
}

StartJobAndTaskArgs StartJobAndTaskArgs::FromJson(const json::value& j)
{
    StartJobAndTaskArgs args(
        JsonHelper<int>::Read("JobId", j.at("m_Item1")),
        JsonHelper<int>::Read("TaskId", j.at("m_Item1")),
        ProcessStartInfo::FromJson(j.at("m_Item2")));

    return std::move(args);
}

