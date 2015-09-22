#include "PeriodicSender.h"
#include "../utils/Logger.h"

using namespace hpc::core;

void PeriodicSender::Start()
{
    if (!this->targetUri.empty())
    {
        pthread_create(&this->threadId, nullptr, MessageThread, this);
    }
}

void PeriodicSender::Stop()
{
    this->isRunning = false;
    if (this->threadId != 0)
    {
        while (this->inRequest) usleep(1);
        pthread_cancel(this->threadId);
        pthread_join(this->threadId, nullptr);
        Logger::Debug("Destructed PeriodicSender {0}", this->targetUri);
    }
}

void* PeriodicSender::MessageThread(void* arg)
{
    pthread_setcancelstate(PTHREAD_CANCEL_ENABLE, nullptr);
    pthread_setcanceltype(PTHREAD_CANCEL_ASYNCHRONOUS, nullptr);

    PeriodicSender* r = static_cast<PeriodicSender*>(arg);
    sleep(r->holdSeconds);

    while (r->isRunning)
    {
        if (!r->targetUri.empty())
        {
            r->inRequest = true;
            r->Send();
            r->inRequest = false;
        }

        sleep(r->intervalSeconds);
    }

    pthread_exit(nullptr);
}

