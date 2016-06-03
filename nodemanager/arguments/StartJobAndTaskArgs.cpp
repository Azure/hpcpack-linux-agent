#include "StartJobAndTaskArgs.h"
#include "../utils/JsonHelper.h"
#include "../utils/Logger.h"

using namespace hpc::arguments;
using namespace hpc::utils;
using namespace web;

StartJobAndTaskArgs::StartJobAndTaskArgs(int jobId, int taskId, ProcessStartInfo&& startInfo,
    std::string&& userName, std::string&& password) :
    JobId(jobId), TaskId(taskId), StartInfo(std::move(startInfo)),
    UserName(std::move(userName)), Password(std::move(password))
{
    //ctor
}

json::value StartJobAndTaskArgs::ToJson() const
{
    json::value v;

    json::value jobTaskId;
    jobTaskId["JobId"] = this->JobId;
    jobTaskId["TaskId"] = this->TaskId;

    v["m_Item1"] = jobTaskId;
    v["m_Item2"] = this->StartInfo.ToJson();
    v["m_Item3"] = json::value::string(this->UserName);
    v["m_Item4"] = json::value::string(this->Password);
    v["m_Item5"] = json::value::string(this->PrivateKey);
    v["m_Item6"] = json::value::string(this->PublicKey);

    return v;
}


StartJobAndTaskArgs StartJobAndTaskArgs::FromJson(const json::value& j)
{
    StartJobAndTaskArgs args(
        JsonHelper<int>::Read("JobId", j.at("m_Item1")),
        JsonHelper<int>::Read("TaskId", j.at("m_Item1")),
        ProcessStartInfo::FromJson(j.at("m_Item2")),
        JsonHelper<std::string>::Read("m_Item3", j),
        JsonHelper<std::string>::Read("m_Item4", j));

    args.PrivateKey = JsonHelper<std::string>::Read("m_Item5", j);
    args.PublicKey = JsonHelper<std::string>::Read("m_Item6", j);
//
//    if (!privateKey.empty())
//    {
//        Logger::Debug("Deserialized privateKey value {0}", privateKey);
//        std::vector<unsigned char> certBytes =
//            utility::conversions::from_base64(privateKey);
//
//        args.Certificate = std::move(certBytes);
//    }

    return std::move(args);
}

