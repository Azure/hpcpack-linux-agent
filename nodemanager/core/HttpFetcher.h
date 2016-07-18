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

        class HttpFetcher : public Reporter<void>
        {
            public:
                HttpFetcher(
                    const std::string& uri,
                    int hold,
                    int interval,
                    std::function<bool(http::http_request&)> requestHandler,
                    std::function<bool(http::http_response&)> responseHandler)
                : Reporter<void>(uri, hold, interval, nullptr),
                requestHandler(requestHandler),
                responseHandler(responseHandler)
                {
                }

                virtual ~HttpFetcher()
                {
                    this->cts.cancel();
                    this->Stop();
                }

                virtual int Report();

            private:
                std::function<bool(http::http_request&)> requestHandler;
                std::function<bool(http::http_response&)> responseHandler;
                pplx::cancellation_token_source cts;
        };
    }
}

#endif // HTTPFETCHER_H
