#ifndef UDPREPORTER_H
#define UDPREPORTER_H

#include <vector>
#include <netdb.h>

#include "Reporter.h"

namespace hpc
{
    namespace core
    {
        class UdpReporter : public Reporter<std::vector<std::vector<unsigned char>>>
        {
            public:
                UdpReporter(
                    const std::string& name,
                    std::function<std::string(pplx::cancellation_token)> getReportUri,
                    int hold,
                    int interval,
                    std::function<std::vector<std::vector<unsigned char>>()> fetcher,
                    std::function<void(int)> onErrorFunc);

                virtual ~UdpReporter();

                virtual int Report();

            protected:
            private:
                void ReConnect();

                std::string uri;
                int s = 0;
                bool initialized = false;
        };
    }
}

#endif // UDPREPORTER_H
