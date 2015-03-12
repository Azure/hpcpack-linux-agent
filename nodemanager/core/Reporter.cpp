#include <cpprest/http_client.h>

#include "Reporter.h"
#include "../utils/Logger.h"

using namespace hpc::core;
using namespace hpc::utils;
using namespace web::http::client;
using namespace web::http;

Reporter::Reporter(const std::string& uri, int interval, std::function<json::value()> fetcher)
    : reportUri(uri), intervalSeconds(interval), valueFetcher(fetcher),
    isRunning(true), inRequest(false)
{
    if (!uri.empty())
    {
        http_client_config config;
        config.set_validate_certificates(false);
        this->client = std::shared_ptr<http_client>(new http_client(uri, config));
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
        pthread_cancel(this->threadId);
        pthread_join(this->threadId, nullptr);
        Logger::Debug("Destructed Reporter {0}", this->reportUri);
    }

    while (this->inRequest);
}

pplx::task<void> Reporter::Report()
{
    const std::string& uri = this->reportUri;
    if (!uri.empty())
    {
        auto jsonBody = this->valueFetcher();
        if (jsonBody.is_null())
        {
            Logger::Info("Skipped reporting to {0} because json is null", uri);
            return pplx::task_from_result();
        }

        if (this->intervalSeconds > 10)
        {
            Logger::Info("---------> Report to {0} with {1}", uri, jsonBody);
        }

        return this->client->request(methods::POST, "", jsonBody, this->cts.get_token()).then([&uri, this](http_response response)
        {
            if (this->intervalSeconds > 10)
            {
                Logger::Debug("---------> Reported to {0} response code {1}", uri, response.status_code());
            }
        });
    }
    else
    {
        return pplx::task_from_result();
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
        r->Report().then([r](auto t)
        {
            try
            {
                t.wait();
            }
            catch (const http_exception& httpEx)
            {
                Logger::Warn("HttpException occurred when report to {0}, ex {1}", r->reportUri, httpEx.what());
            }
            catch (const std::exception& ex)
            {
                Logger::Error("Exception occurred when report to {0}, ex {1}", r->reportUri, ex.what());
                //exit(-1);
            }
            catch (...)
            {
                Logger::Error("Unknown error occurred when report to {0}", r->reportUri);
                //exit(-1);
            }

            r->inRequest = false;
        });

        sleep(r->intervalSeconds);
   }

    pthread_exit(nullptr);
}
