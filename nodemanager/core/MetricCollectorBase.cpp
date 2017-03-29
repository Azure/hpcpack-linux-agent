#include "MetricCollectorBase.h"

using namespace hpc::core;

void MetricCollectorBase::ApplyConfig(const MetricCounter& config)
{
    this->metricId = config.MetricId;

    if (config.InstanceName.empty() && this->instanceNamesFunc)
    {
        try
        {
            auto instanceNames = this->instanceNamesFunc();
            Logger::Debug("queried instanceNames '{0}'", String::Join<','>(instanceNames));

            if (!instanceNames.empty())
            {
                auto client = HttpHelper::GetHttpClient(NodeManagerConfig::ResolveMetricInstanceIdsUri());

                json::value jsonBody = JsonHelper<std::vector<std::string>>::ToJson(instanceNames);
                auto request = HttpHelper::GetHttpRequest(web::http::methods::POST, jsonBody);

                client->request(*request).then([instanceNames, this](pplx::task<web::http::http_response> t)
                {
                    auto response = t.get().extract_json().then([instanceNames, this](pplx::task<json::value> t)
                    {
                        auto jsonIds = t.get();
                        std::vector<int> ids = JsonHelper<std::vector<int>>::FromJson(jsonIds);
                        Logger::Debug("Queued instance ids {0} for instance names {1}", String::Join<','>(ids), String::Join<','>(instanceNames));
                        if (ids.size() != instanceNames.size())
                        {
                            Logger::Error(
                                "queried ids size {0} != instanceNames size {1}, ids '{2}', names '{3}'",
                                ids.size(), instanceNames.size(), String::Join<','>(ids), String::Join<','>(instanceNames));

                            this->enabled = false;
                        }
                        else
                        {
                            this->cachedInstanceIds.clear();
                            for (size_t i = 0; i < ids.size(); i++)
                            {
                                this->cachedInstanceIds[ids[i]] = instanceNames[i];
                            }

                            this->enabled = true;
                        }
                    }).then([instanceNames](pplx::task<void> t)
                    {
                        try
                        {
                            t.wait();
                        }
                        catch (const std::exception& ex)
                        {
                            Logger::Error("Unabled to get instance ids for instance names {0}, ex {1}", String::Join<','>(instanceNames), ex.what());
                        }
                    });
                }).then([instanceNames](pplx::task<void> t)
                {
                    try
                    {
                        t.wait();
                    }
                    catch (const std::exception& ex)
                    {
                        Logger::Error("Error when query instance ids for {0}, ex {1}, resetting naming cache", String::Join<','>(instanceNames), ex.what());
                        NamingClient::InvalidateCache();
                    }
                });
            }
            else
            {
                Logger::Warn(
                    "No instances returned for metric {0}",
                    config.MetricId);

                this->cachedInstanceIds[config.InstanceId] = config.InstanceName;
                this->enabled = true;
            }
        }
        catch (const std::exception& ex)
        {
            Logger::Error("Exception happened while applying Config for {0}, ex {1}", config.MetricId, ex.what());
        }
    }
    else
    {
        Logger::Debug("Config instance name {0}, instance id {1}", config.InstanceName, config.InstanceId);
        this->cachedInstanceIds[config.InstanceId] = config.InstanceName;
        this->enabled = true;
    }
}
