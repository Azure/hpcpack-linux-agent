#ifndef REPORTER_H
#define REPORTER_H

#include <cpprest/json.h>
#include <functional>

#include "../utils/Logger.h"

using namespace hpc::utils;

namespace hpc
{
    namespace core
    {
        template<typename ReportType>
        class Reporter
        {
            public:
                Reporter(const std::string& uri, int hold, int interval, std::function<ReportType()> fetcher)
                    : reportUri(uri), valueFetcher(fetcher), intervalSeconds(interval), holdSeconds(hold)
                {
                }

                void Start()
                {
                    if (!this->reportUri.empty())
                    {
                        pthread_create(&this->threadId, nullptr, ReportingThread, this);
                    }
                }

                void Stop()
                {
                    this->isRunning = false;
                    if (this->threadId != 0)
                    {
                        while (this->inRequest) usleep(1);
                        pthread_cancel(this->threadId);
                        pthread_join(this->threadId, nullptr);
                        Logger::Debug("Destructed Reporter {0}", this->reportUri);
                    }
                }

                virtual ~Reporter()
                {
                    Logger::Debug("Destruct Reporter {0}", this->reportUri);
                }

                virtual int Report() = 0;

            protected:
                const std::string reportUri;
                std::function<ReportType()> valueFetcher;
                int intervalSeconds;

            private:
                static void* ReportingThread(void* arg)
                {
                    pthread_setcancelstate(PTHREAD_CANCEL_ENABLE, nullptr);
                    pthread_setcanceltype(PTHREAD_CANCEL_ASYNCHRONOUS, nullptr);

                    Reporter* r = static_cast<Reporter*>(arg);
                    sleep(r->holdSeconds);

                    while (r->isRunning)
                    {
                        bool needRetry = false;
                        if (!r->reportUri.empty())
                        {
                            r->inRequest = true;
                            needRetry = (0 != r->Report());
                            r->inRequest = false;
                        }

                        sleep(needRetry ? r->ErrorRetrySeconds : r->intervalSeconds);
                    }

                    pthread_exit(nullptr);
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
