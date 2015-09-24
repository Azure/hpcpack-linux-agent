#ifndef METRICCOLLECTORBASE_H
#define METRICCOLLECTORBASE_H

#include <vector>
#include <map>

#include "../data/Umid.h"
#include "../arguments/MetricCounter.h"

namespace hpc
{
    namespace core
    {
        using namespace hpc::data;
        using namespace hpc::arguments;

        class MetricCollectorBase
        {
            public:
                MetricCollectorBase(std::function<float(const std::string&)> func)
                    : collectFunc(func)
                {
                }

                MetricCollectorBase() = default;

                void ApplyConfig(const MetricCounter& config)
                {
                    this->metricId = config.MetricId;

                    this->cachedInstanceIds[config.InstanceId] = config.InstanceName;
                    if (config.InstanceName.empty())
                    {
                        // TODO: get instance ids by metric.
                    }

                    this->enabled = true;
                }

                void Reset()
                {
                    this->enabled = false;
                    this->cachedInstanceIds.clear();
                }

                bool IsInstanceLevelMetric() { return false; }

                bool IsEnabled() const { return this->enabled; }

                virtual std::vector<std::pair<float, Umid>> CollectValues()
                {
                    std::vector<std::pair<float, Umid>> results;

                    std::transform(cachedInstanceIds.cbegin(), cachedInstanceIds.cend(), std::back_inserter(results), [this] (auto& i)
                    {
                        return std::make_pair<float, Umid>(
                            this->collectFunc(i.second),
                            Umid(this->metricId, i.first));
                    });

                    return std::move(results);
                }

            private:
                uint16_t metricId;
                bool enabled = false;
                std::function<float(const std::string&)> collectFunc;
                std::map<uint16_t, std::string> cachedInstanceIds;
        };
    }
}

#endif // METRICCOLLECTORBASE_H
