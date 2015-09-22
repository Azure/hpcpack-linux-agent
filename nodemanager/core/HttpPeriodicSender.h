#ifndef HTTPPERIODICSENDER_H
#define HTTPPERIODICSENDER_H

#include <cpprest/json.h>
#include <cpprest/http_client.h>
#include <functional>
#include "PeriodicSender.h"

namespace hpc
{
    namespace core
    {
        using namespace web;

        class HttpPeriodicSender : public PeriodicSender
        {
            public:
                HttpPeriodicSender(
                    const std::string& uri,
                    int hold,
                    int interval,
                    const http::method& mtd,
                    std::function<void(http::http_request&)> requestHandler,
                    std::function<void(http::http_response&)> responseHandler)
                : PeriodicSender(uri, hold, interval), mtd(mtd),requestHandler(requestHandler),responseHandler(responseHandler)
                {
                }

                virtual ~HttpPeriodicSender()
                {
                    this->cts.cancel();
                    this->Stop();
                }
                
                virtual void Send();

            protected:
                http::method mtd;
            private:
                std::function<bool(http::http_request&)> requestHandler;
                std::function<bool(http::http_response&)> responseHandler;
                pplx::cancellation_token_source cts;
        };
    }
}

#endif // HTTPPERIODICSENDER_H