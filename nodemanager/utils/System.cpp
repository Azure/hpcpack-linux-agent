#include <string>
#include <sys/types.h>
#include <ifaddrs.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <iostream>

#include "System.h"
#include "Logger.h"

namespace hpc
{
    namespace utils
    {
        std::string System::GetIpAddress(IpAddressVersion version, const std::string& name)
        {
            ifaddrs *ifAddr = nullptr;
            bool isV4 = version == IpAddressVersion::V4;
            const int Family = isV4 ? AF_INET : AF_INET6;

            getifaddrs(&ifAddr);

            std::string ip;

            for (ifaddrs *i = ifAddr; i != nullptr; i = i->ifa_next)
            {
                if (!i->ifa_addr) continue;

                if (i->ifa_addr->sa_family == Family)
                {
                    void* tmp = isV4 ?
                        (void*)&((sockaddr_in*)i->ifa_addr)->sin_addr :
                        (void*)&((sockaddr_in6*)i->ifa_addr)->sin6_addr;

                    if (std::string(i->ifa_name) != name) continue;

                    const int BufferLength = isV4 ? INET_ADDRSTRLEN : INET6_ADDRSTRLEN;
                    char buffer[BufferLength];
                    inet_ntop(Family, tmp, buffer, BufferLength);

                    ip = buffer;
                    Logger::Debug("Found IPAddress {0}, name {1}", ip, name);
                }
            }

            if (ifAddr != nullptr) freeifaddrs(ifAddr);

            return std::move(ip);
        }
    }
}
