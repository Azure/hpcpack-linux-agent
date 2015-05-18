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

                virtual ~Reporter()
                {
                    Logger::Debug("Destruct Reporter {0}", this->reportUri);

                    this->isRunning = false;
                    if (this->threadId != 0)
                    {
                        while (this->inRequest) usleep(1);
                        pthread_cancel(this->threadId);
                        pthread_join(this->threadId, nullptr);
                        Logger::Debug("Destructed Reporter {0}", this->reportUri);
                    }
                }

                virtual void Report() = 0;

            protected:
                const std::string reportUri;
                std::function<ReportType()> valueFetcher;

            private:
                static void* ReportingThread(void* arg)
                {
                    pthread_setcancelstate(PTHREAD_CANCEL_ENABLE, nullptr);
                    pthread_setcanceltype(PTHREAD_CANCEL_ASYNCHRONOUS, nullptr);

                    Reporter* r = static_cast<Reporter*>(arg);
                    sleep(r->holdSeconds);

                    while (r->isRunning)
                    {
                        if (!r->reportUri.empty())
                        {
                            r->inRequest = true;
                            r->Report();
                            r->inRequest = false;
                        }

                        sleep(r->intervalSeconds);
                    }

                    pthread_exit(nullptr);
                }

                int intervalSeconds;
                int holdSeconds;

                pthread_t threadId;
                bool isRunning = true;
                bool inRequest = false;
        };
    }
}

#endif // REPORTER_H
