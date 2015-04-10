#include "StartJobAndTaskArgs.h"
#include "../utils/JsonHelper.h"
#include "../utils/Logger.h"

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
        JsonHelper<std::string>::Read("m_Item3", j),
        JsonHelper<std::string>::Read("m_Item4", j));

    auto cert = JsonHelper<std::string>::Read("m_Item5", j);
    if (!cert.empty())
    {
        Logger::Debug("Deserialized cert value {0}", cert);
        std::vector<unsigned char> certBytes =
            utility::conversions::from_base64(cert);

        args.Certificate = std::move(certBytes);
    }

    return std::move(args);
}

