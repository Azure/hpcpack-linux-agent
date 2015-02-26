#include "JobInfo.h"

using namespace web;
using namespace hpc::data;

json::value JobInfo::ToJson() const
{
    json::value j;

    j["JobId"] = this->JobId;

    std::vector<json::value> tasks;

    std::transform(this->Tasks.cbegin(), this->Tasks.cend(), std::back_inserter(tasks), [](auto i) { return i.second->ToJson(); });

    j["Tasks"] = json::value::array(tasks);

    return std::move(j);
}
