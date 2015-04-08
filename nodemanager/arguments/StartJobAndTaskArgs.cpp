#include "StartJobAndTaskArgs.h"
#include "../utils/JsonHelper.h"

using namespace hpc::arguments;
using namespace hpc::utils;

StartJobAndTaskArgs::StartJobAndTaskArgs(int jobId, int taskId, ProcessStartInfo&& startInfo,
    std::string&& userName, std::string&& password) :
    JobId(jobId), TaskId(taskId), StartInfo(std::move(startInfo)),
    UserName(std::move(userName)), Password(std::move(password))
{
    //ctor
}

StartJobAndTaskArgs StartJobAndTaskArgs::FromJson(const json::value& j)
{
    StartJobAndTaskArgs args(
        JsonHelper<int>::Read("JobId", j.at("m_Item1")),
        JsonHelper<int>::Read("TaskId", j.at("m_Item1")),
        ProcessStartInfo::FromJson(j.at("m_Item2")),
        JsonHelper<std::string>::FromJson(j.at("m_Item3")),
        JsonHelper<std::string>::FromJson(j.at("m_Item4")));

    auto cert = j.at("m_Item5");
    if (!cert.is_null())
    {

    }

    return std::move(args);
}

