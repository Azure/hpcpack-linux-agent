#include "StartTaskArgs.h"
#include "../utils/JsonHelper.h"

using namespace hpc::arguments;
using namespace hpc::utils;

StartTaskArgs::StartTaskArgs(int jobId, int taskId, ProcessStartInfo&& startInfo) :
    JobId(jobId), TaskId(taskId), StartInfo(std::move(startInfo))
{
    //ctor
}

StartTaskArgs StartTaskArgs::FromJson(const json::value& j)
{
    StartTaskArgs args(
        JsonHelper<int>::Read("JobId", j.at("m_Item1")),
        JsonHelper<int>::Read("TaskId", j.at("m_Item1")),
        ProcessStartInfo::FromJson(j.at("m_Item2")));

    return std::move(args);
}

