#ifndef METRICCOUNTER_H
#define METRICCOUNTER_H

#include <string>
#include <cpprest/json.h>

namespace hpc
{
    namespace arguments
    {
        class MetricCounter
        {
            public:
                MetricCounter() = default;

                MetricCounter(const std::string& path, const int metricId, const int instanceId)
                    : Path(path), MetricId(metricId), InstanceId(instanceId)
                {
                }

                std::string Path;
                int MetricId;
                int InstanceId;

                static MetricCounter FromJson(const web::json::value& jsonValue);
        };
    }
}

#endif // METRICCOUNTER_H
