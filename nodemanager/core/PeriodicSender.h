#ifndef PERIODICSENDER_H
#define PERIODICSENDER_H

#include <functional>
#include "../utils/Logger.h"

using namespace hpc::utils;

namespace hpc
{
    namespace core
    {
        class PeriodicSender
        {
            public:
                PeriodicSender(const std::string& uri, int hold, int interval)
                    : targetUri(uri), intervalSeconds(interval), holdSeconds(hold)
                {
                }
                virtual ~PeriodicSender()
                {
                    Logger::Debug("Destruct PeriodicSender {0}", this->targetUri);
                }

                void Start();
                void Stop();
                void SetInterval(int intervalInSeconds)
                {
                    this->intervalSeconds = intervalInSeconds;
                }
                virtual void Send() = 0;

            protected:
                const std::string targetUri;
                int intervalSeconds;

            private:
                static void* MessageThread(void* arg);


                int holdSeconds;

                pthread_t threadId = 0;
                bool isRunning = true;
                bool inRequest = false;
        };
    }
}

#endif // PERIODICSENDER_H
