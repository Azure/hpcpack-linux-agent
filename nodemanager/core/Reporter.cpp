#include <cpprest/http_client.h>

#include "Reporter.h"
#include "../utils/Logger.h"

using namespace hpc::core;
using namespace hpc::utils;
using namespace web::http::client;
using namespace web::http;

Reporter::Reporter(const std::string& uri, int interval, std::function<json::value()> fetcher)
    : reportUri(uri), intervalSeconds(interval), valueFetcher(fetcher),
    isRunning(true)
{
    if (!uri.empty())
    {
        pthread_create(&this->threadId, nullptr, ReportingThread, this);
    }
}

Reporter::~Reporter()
{
    Logger::Debug("Destruct Reporter {0}", this->reportUri);

    this->isRunning = false;
    this->cts.cancel();
    if (this->threadId != 0)
    {
        while (this->inRequest) usleep(1);
        pthread_cancel(this->threadId);
        pthread_join(this->threadId, nullptr);
        Logger::Debug("Destructed Reporter {0}", this->reportUri);
    }
}

void Reporter::Report()
{
    const std::string& uri = this->reportUri;
    if (!uri.empty())
    {
        auto jsonBody = this->valueFetcher();
        if (jsonBody.is_null())
        {
            Logger::Info("Skipped reporting to {0} because json is null", uri);
            return;
        }

        if (this->intervalSeconds > 10)
        {
            Logger::Info("---------> Report to {0} with {1}", uri, jsonBody);
        }

        http_client_config config;
        config.set_validate_certificates(false);
        utility::seconds timeout(5l);
        config.set_timeout(timeout);
        http_client client(uri, config);

        try
        {
            http_response response = client.request(methods::POST, "", jsonBody, this->cts.get_token()).get();

            if (this->intervalSeconds > 10)
            {
                Logger::Debug("---------> Reported to {0} response code {1}", uri, response.status_code());
            }
        }
        catch (const http_exception& httpEx)
        {
            Logger::Warn("HttpException occurred when report to {0}, ex {1}", this->reportUri, httpEx.what());
        }
        catch (const std::exception& ex)
        {
            Logger::Error("Exception occurred when report to {0}, ex {1}", this->reportUri, ex.what());
        }
        catch (...)
        {
            Logger::Error("Unknown error occurred when report to {0}", this->reportUri);
        }
    }
}

void* Reporter::ReportingThread(void * arg)
{
    pthread_setcancelstate(PTHREAD_CANCEL_ENABLE, nullptr);
    pthread_setcanceltype(PTHREAD_CANCEL_ASYNCHRONOUS, nullptr);

    Reporter* r = static_cast<Reporter*>(arg);

    while (r->isRunning)
    {
        r->inRequest = true;
        r->Report();
        r->inRequest = false;

        sleep(r->intervalSeconds);
    }

    pthread_exit(nullptr);
}
