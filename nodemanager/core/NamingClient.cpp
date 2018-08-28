#include "NamingClient.h"
#include "NodeManagerConfig.h"
#include "../utils/Logger.h"
#include "../utils/ReaderLock.h"
#include "HttpHelper.h"
#include <stdlib.h>

using namespace web::http;
using namespace web::http::client;
using namespace hpc::core;
using namespace hpc::utils;

void NamingClient::InvalidateCache()
{
    auto instance = GetInstance(NodeManagerConfig::GetNamingServiceUri());

    if (instance)
    {
        WriterLock writerLock(&instance->lock);

        Logger::Debug("ResolveServiceLocation> Cleared.");
        instance->serviceLocations.clear();
    }
}

std::string NamingClient::GetServiceLocation(const std::string& serviceName, pplx::cancellation_token token)
{
    std::map<std::string, std::string>::iterator location;

    {
        ReaderLock readerLock(&this->lock);
        location = this->serviceLocations.find(serviceName);
    }

    if (location == this->serviceLocations.end())
    {
        {
            ReaderLock readerLock(&this->lock);
            Logger::Debug("ResolveServiceLocation> there is no entry for {0}", serviceName);
            for (auto kvp : this->serviceLocations)
            {
                Logger::Debug("ResolveServiceLocation> entry {0} = {1}", kvp.first, kvp.second);
            }

            location = this->serviceLocations.find(serviceName);
        }

        if (location == this->serviceLocations.end())
        {
            std::string temp;
            this->RequestForServiceLocation(serviceName, temp, token);

            {
                WriterLock writerLock(&this->lock);

                this->serviceLocations[serviceName] = temp;
                location = this->serviceLocations.find(serviceName);
            }
        }
    }

    Logger::Info("ResolveServiceLocation> Resolved serviceLocation {0} for {1}", location->second, serviceName);
    return location->second;
}

void NamingClient::RequestForServiceLocation(const std::string& serviceName, std::string& serviceLocation, pplx::cancellation_token token)
{
    int selected = rand() % this->namingServicesUri.size();
    std::string uri;
    int interval = this->intervalSeconds;

    while (!token.is_canceled())
    {
        try
        {
            selected %= this->namingServicesUri.size();
            uri = this->namingServicesUri[selected++] + serviceName;
            Logger::Debug("ResolveServiceLocation> Fetching from {0}", uri);
            auto client = HttpHelper::GetHttpClient(uri);

            auto request = HttpHelper::GetHttpRequest(methods::GET);
            http_response response = client->request(*request, token).get();
            if (response.status_code() == http::status_codes::OK)
            {
                serviceLocation = JsonHelper<std::string>::FromJson(response.extract_json().get());
                Logger::Debug("ResolveServiceLocation> Fetched from {0} response code {1}, location {2}", uri, response.status_code(), serviceLocation);
                return;
            }
            else
            {
                Logger::Debug("ResolveServiceLocation> Fetched from {0} response code {1}", uri, response.status_code());
            }
        }
        catch (const http_exception& httpEx)
        {
            Logger::Warn("ResolveServiceLocation> HttpException occurred when fetching from {0}, ex {1}", uri, httpEx.what());
        }
        catch (const std::exception& ex)
        {
            Logger::Error("ResolveServiceLocation> Exception occurred when fetching from {0}, ex {1}", uri, ex.what());
        }
        catch (...)
        {
            Logger::Error("ResolveServiceLocation> Unknown error occurred when fetching from {0}", uri);
        }

        if (token.is_canceled()) break;

        sleep(interval);
        interval *= 2;
        if (interval > 60) interval = 60;
    }
}
