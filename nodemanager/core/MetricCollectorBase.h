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
                MetricCollectorBase(std::function<float(const std::string&)> collecter, std::function<std::vector<std::string>(const std::string&)> instanceNameQuerier = std::function<std::vector<std::string>(const std::string&)>())
                    : collectFunc(collecter), instanceNamesFunc(instanceNameQuerier)
                {
                }

                MetricCollectorBase() = default;

                void ApplyConfig(const MetricCounter& config, pplx::cancellation_token token);

                void Reset()
                {
                    this->enabled = false;
                    this->counters.clear();
                }

                bool IsInstanceLevelMetric() { return this->isInstanceLevelMetric; }

                bool IsEnabled() const { return this->enabled; }

                virtual std::vector<std::pair<float, Umid>> CollectValues()
                {
                    std::vector<std::pair<float, Umid>> results;
                    for (const auto & counter : counters)
                    {
                        auto metricId = counter.first;
                        for (const auto & instance : counter.second)
                        {
                            auto instanceId = instance.first;
                            auto instanceName = instance.second;
                            results.push_back(std::make_pair<float, Umid>(this->collectFunc(instanceName), Umid(metricId, instanceId)));
                        }
                    }

                    return std::move(results);
                }

            private:
                bool enabled = false;
                bool isInstanceLevelMetric = false;
                std::function<float(const std::string&)> collectFunc;
                std::function<std::vector<std::string>(const std::string&)> instanceNamesFunc;
                std::map<uint16_t, std::map<uint16_t, std::string>> counters;
        };
    }
}

#endif // METRICCOLLECTORBASE_H
