#ifndef UDPREPORTER_H
#define UDPREPORTER_H

#include <vector>
#include <netdb.h>

#include "Reporter.h"

namespace hpc
{
    namespace core
    {
        class UdpReporter : public Reporter<std::vector<unsigned char>>
        {
            public:
                UdpReporter(
                    const std::string& uri,
                    int hold,
                    int interval,
                    std::function<std::vector<unsigned char>()> fetcher);

                virtual ~UdpReporter();

                virtual void Report();

            protected:
            private:
                int s;
                bool initialized = false;
        };
    }
}

#endif // UDPREPORTER_H
