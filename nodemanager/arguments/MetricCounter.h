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

                MetricCounter(const std::string& path, const int metricId, const int instanceId, const std::string& name)
                    : Path(path), MetricId(metricId), InstanceId(instanceId), InstanceName(name)
                {
                }

                std::string Path;
                int MetricId;
                int InstanceId;
                std::string InstanceName;

                static MetricCounter FromJson(const web::json::value& jsonValue);
        };
    }
}

#endif // METRICCOUNTER_H
