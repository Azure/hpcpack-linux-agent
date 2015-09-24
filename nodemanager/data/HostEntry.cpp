#include "HostEntry.h"
#include "../utils/JsonHelper.h"

using namespace hpc::data;
using namespace hpc::utils;

HostEntry HostEntry::FromJson(const web::json::value& jsonValue)
{
    return HostEntry(
        JsonHelper<std::string>::Read("Name", jsonValue),
        JsonHelper<std::string>::Read("Address", jsonValue));
}

namespace hpc
{
    namespace utils
    {
        template <>
        HostEntry JsonHelper<HostEntry>::FromJson(const json::value& j)
        {
            if (!j.is_null()) { return HostEntry::FromJson(j); }
            else { return HostEntry(); }
        }
    }
}
