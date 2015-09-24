#ifndef HTTPFETCHER_H
#define HTTPFETCHER_H

#include <cpprest/json.h>
#include <cpprest/http_client.h>
#include <functional>
#include "Reporter.h"

namespace hpc
{
    namespace core
    {
        using namespace web;

        class HttpFetcher : public Reporter<int>
        {
            public:
                HttpFetcher(
                    const std::string& uri,
                    int hold,
                    int interval,
                    std::function<void(http::http_request&)> requestHandler,
                    std::function<void(http::http_response&)> responseHandler)
                : Reporter<json::value>(uri, hold, interval, nullptr),requestHandler(requestHandler),responseHandler(responseHandler)
                {
                }

                virtual ~HttpFetcher()
                {
                    this->cts.cancel();
                    this->Stop();
                }
                
                virtual void Report();

            private:
                std::function<bool(http::http_request&)> requestHandler;
                std::function<bool(http::http_response&)> responseHandler;
                pplx::cancellation_token_source cts;
        };
    }
}

#endif // HTTPFETCHER_H