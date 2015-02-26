#include <cpprest/http_client.h>

#include "Reporter.h"
#include "../utils/Logger.h"

using namespace hpc::core;
using namespace hpc::utils;
using namespace web::http::client;
using namespace web::http;

Reporter::Reporter(const std::string& uri, int interval, std::function<json::value()> fetcher) : reportUri(uri), intervalSeconds(interval), valueFetcher(fetcher)
{
    pthread_create(&this->threadId, nullptr, ReportingThread, this);
}

Reporter::~Reporter()
{
    Logger::Debug("Destruct Reporter {0}", this->reportUri);
    if (this->threadId != 0)
    {
        pthread_cancel(this->threadId);
        pthread_join(this->threadId, nullptr);
    }
}

pplx::task<void> Reporter::Report()
{
    std::string uri = this->reportUri;
    if (!uri.empty())
    {
        auto jsonBody = this->valueFetcher();
        Logger::Debug("Report to {0} with {1}", uri, jsonBody);

        http_client client(uri);
        return client.request(methods::POST, "", jsonBody).then([&uri](http_response response)
        {
            Logger::Info("Report to {0} response code {1}", uri, response.status_code());
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

    while (true)
    {
        r->Report().wait();

        sleep(r->intervalSeconds);
    }

    pthread_exit(nullptr);
}
