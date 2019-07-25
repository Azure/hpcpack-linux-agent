#ifndef HTTPREPORTER_H
#define HTTPREPORTER_H

#include <cpprest/json.h>
#include <cpprest/http_client.h>
#include <functional>

#include "Reporter.h"

namespace hpc
{
    namespace core
    {
        using namespace web;

        class HttpReporter : public Reporter<json::value>
        {
            public:
                HttpReporter(
                    const std::string& reporterName,
                    std::function<std::string(pplx::cancellation_token)> getUri,
                    int hold,
                    int interval,
                    std::function<json::value()> fetcher,
                    std::function<void(int)> onErrorFunc, 
                    int retryFactor = 2)
                : Reporter<json::value>(reporterName, getUri, hold, interval, fetcher, onErrorFunc, retryFactor)
                {
                }

                virtual ~HttpReporter()
                {
                    this->cts.cancel();
                    this->Stop();
                }

                virtual int Report();

            protected:
            private:
        };
    }
}

#endif // HTTPREPORTER_H
