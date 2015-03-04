#ifndef REPORTER_H
#define REPORTER_H

#include <cpprest/json.h>
#include <functional>

using namespace web;

namespace hpc
{
    namespace core
    {
        class Reporter
        {
            public:
                Reporter(const std::string& uri, int interval, std::function<json::value()> fetcher);
                ~Reporter();

                pplx::task<void> Report();
            protected:
            private:
                static void* ReportingThread(void* arg);

                const std::string reportUri;
                int intervalSeconds;
                std::function<json::value()> valueFetcher;

                pthread_t threadId;
                pplx::cancellation_token_source cts;
                bool isRunning;
                std::shared_ptr<http::client::http_client> client;
        };
    }
}

#endif // REPORTER_H
