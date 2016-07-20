#ifndef METRICCOLLECTORBASE_H
#define METRICCOLLECTORBASE_H

#include <vector>
#include <map>
#include <cpprest/json.h>
#include <cpprest/http_client.h>

#include "HttpHelper.h"
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
                MetricCollectorBase(std::function<float(const std::string&)> collecter, std::function<std::vector<std::string>()> instanceNameQuerier = std::function<std::vector<std::string>()>())
                    : collectFunc(collecter), instanceNamesFunc(instanceNameQuerier)
                {
                }

                MetricCollectorBase() = default;

                void ApplyConfig(const MetricCounter& config);

                void Reset()
                {
                    this->enabled = false;
                    this->cachedInstanceIds.clear();
                }

                bool IsInstanceLevelMetric() { return this->isInstanceLevelMetric; }

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
                bool isInstanceLevelMetric = false;
                std::function<float(const std::string&)> collectFunc;
                std::function<std::vector<std::string>()> instanceNamesFunc;
                std::map<uint16_t, std::string> cachedInstanceIds;
        };
    }
}

#endif // METRICCOLLECTORBASE_H
