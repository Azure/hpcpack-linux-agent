#include "NodeInfo.h"
#include "../utils/System.h"

using namespace web;
using namespace hpc::data;
using namespace hpc::utils;

NodeInfo::NodeInfo()
{
    this->Name = System::GetNodeName();
}

json::value NodeInfo::ToJson()
{
    json::value j;
    j["Availability"] = json::value::number((int)this->Availability);
    j["JustStarted"] = this->JustStarted;
    j["MacAddress"] = json::value::string(this->MacAddress);
    j["Name"] = json::value::string(this->Name);

    std::vector<json::value> jobs;

    std::transform(this->Jobs.cbegin(), this->Jobs.cend(), std::back_inserter(jobs), [](auto i) { return i.second->ToJson(); });

    j["Jobs"] = json::value::array(jobs);

    this->JustStarted = false;
    return std::move(j);
}
