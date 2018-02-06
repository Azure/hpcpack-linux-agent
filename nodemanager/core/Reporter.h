#ifndef REPORTER_H
#define REPORTER_H

#include <cpprest/json.h>
#include <functional>

#include "../utils/Logger.h"
#include "NamingClient.h"

using namespace hpc::utils;

namespace hpc
{
    namespace core
    {
        template<typename ReportType>
        class Reporter
        {
            public:
                Reporter(std::string reporterName, std::function<std::string(pplx::cancellation_token)> getUri, int hold, int interval, std::function<ReportType()> fetcher, std::function<void()> onErrorFunc)
                    : name(reporterName), getReportUri(getUri), valueFetcher(fetcher), onError(onErrorFunc), intervalSeconds(interval), holdSeconds(hold)
                {
                }

                void Start()
                {
                    if (this->getReportUri)
                    {
                        pthread_create(&this->threadId, nullptr, ReportingThread, this);
                    }
                }

                void Stop()
                {
                    Logger::Debug("Stopping the thread of Reporter {0}", this->name);
                    this->isRunning = false;
                    this->cts.cancel();
                    if (this->threadId != 0)
                    {
                        while (this->inRequest) usleep(1);
                        pthread_join(this->threadId, nullptr);
                        Logger::Debug("Stopped the thread of Reporter {0}", this->name);
                    }
                }

                virtual ~Reporter()
                {
                    Logger::Debug("Destructed Reporter {0}", this->name);
                }

                virtual int Report() = 0;

            protected:
                std::string name;
                std::function<std::string(pplx::cancellation_token)> getReportUri;
                std::function<ReportType()> valueFetcher;
                std::function<void()> onError;
                int intervalSeconds;
                pplx::cancellation_token_source cts;

            private:
                static void* ReportingThread(void* arg)
                {
                    Reporter* r = static_cast<Reporter*>(arg);
                    sleep(r->holdSeconds);

                    while (r->isRunning)
                    {
                        bool needRetry = false;
                        if (r->getReportUri)
                        {
                            r->inRequest = true;
                            if ((needRetry = (0 != r->Report())))
                            {
                                if (r->onError)
                                {
                                    r->onError();
                                }
                            }

                            r->inRequest = false;
                        }

                        if (r->isRunning) sleep(needRetry ? r->ErrorRetrySeconds : r->intervalSeconds);
                    }

                    return nullptr;
                }

                const int ErrorRetrySeconds = 2;

                int holdSeconds;

                pthread_t threadId = 0;
                bool isRunning = true;
                bool inRequest = false;
        };
    }
}

#endif // REPORTER_H
