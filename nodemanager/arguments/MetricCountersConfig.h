#ifndef METRICCOUNTERSCONFIG_H
#define METRICCOUNTERSCONFIG_H

#include "MetricCounter.h"
#include "../utils/JsonHelper.h"

namespace hpc
{
    namespace arguments
    {
        using namespace hpc::utils;

        class MetricCountersConfig
        {
            public:
                MetricCountersConfig(std::vector<MetricCounter>&& metricCounters)
                    : MetricCounters(std::move(metricCounters))
                {
                }

                std::vector<MetricCounter> MetricCounters;

                static MetricCountersConfig FromJson(const web::json::value& jsonValue)
                {
                    return MetricCountersConfig(
                        JsonHelper<std::vector<MetricCounter>>::Read("MetricCounters", jsonValue));
                }

            protected:
            private:
        };
    }
}

#endif // METRICCOUNTERSCONFIG_H
