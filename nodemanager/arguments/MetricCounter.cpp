#include "MetricCounter.h"
#include "../utils/JsonHelper.h"

using namespace hpc::arguments;
using namespace hpc::utils;

MetricCounter MetricCounter::FromJson(const web::json::value& jsonValue)
{
// TODO: check enum type conversion.
    return MetricCounter(
        JsonHelper<std::string>::Read("Path", jsonValue),
        JsonHelper<int>::Read("MetricId", jsonValue),
        JsonHelper<int>::Read("InstanceId", jsonValue),
        JsonHelper<std::string>::Read("InstanceName", jsonValue));
}

namespace hpc
{
    namespace utils
    {
        template <>
        MetricCounter JsonHelper<MetricCounter>::FromJson(const json::value& j)
        {
            if (!j.is_null()) { return MetricCounter::FromJson(j); }
            else { return MetricCounter(); }
        }
    }
}
