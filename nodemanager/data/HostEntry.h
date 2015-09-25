#ifndef HOSTENTRY_H
#define HOSTENTRY_H

#include <string>
#include <cpprest/json.h>

namespace hpc
{
    namespace data
    {
        class HostEntry
        {
            public:
                HostEntry() = default;

                HostEntry(const std::string& hostName, const std::string& ipAddress):
                    HostName(hostName), IPAddress(ipAddress)
                {
                }

                static HostEntry FromJson(const web::json::value& jsonValue);

                std::string HostName;
                std::string IPAddress;
        };
    }
}

#endif // HOSTENTRY_H
