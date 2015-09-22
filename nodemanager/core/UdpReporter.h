#ifndef UDPREPORTER_H
#define UDPREPORTER_H

#include <vector>
#include <netdb.h>

#include "PeriodicSender.h"

namespace hpc
{
    namespace core
    {
        class UdpReporter : public PeriodicSender
        {
            public:
                UdpReporter(
                    const std::string& uri,
                    int hold,
                    int interval,
                    std::function<std::vector<unsigned char>()> fetcher);

                virtual ~UdpReporter();

                virtual void Send();

            protected:
                std::function<std::vector<unsigned char>()> valueFetcher;

            private:
                int s;
                bool initialized = false;
        };
    }
}

#endif // UDPREPORTER_H
