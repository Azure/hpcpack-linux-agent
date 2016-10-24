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
                Reporter(std::string reporterName, std::function<std::string()> getUri, int hold, int interval, std::function<ReportType()> fetcher)
                    : name(reporterName), getReportUri(getUri), valueFetcher(fetcher), intervalSeconds(interval), holdSeconds(hold)
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
                    this->isRunning = false;
                    if (this->threadId != 0)
                    {
                        while (this->inRequest) usleep(1);
                        pthread_cancel(this->threadId);
                        pthread_join(this->threadId, nullptr);
                        Logger::Debug("Destructed Reporter {0}", this->name);
                    }
                }

                virtual ~Reporter()
                {
                    Logger::Debug("Destruct Reporter {0}", this->name);
                }

                virtual int Report() = 0;

            protected:
                std::string name;
                std::function<std::string()> getReportUri;
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
                        if (r->getReportUri)
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
