#ifndef SYSTEM_H
#define SYSTEM_H

#include <string>

namespace hpc
{
    namespace utils
    {
        enum class IpAddressVersion
        {
            V4 = 4,
            V6 = 6
        };

        struct System
        {
            public:
                static std::string GetIpAddress(IpAddressVersion version, const std::string& name);
                static void CPUUsage(long int &total, long int &idle);
                static void Memory(unsigned long &available, unsigned long &total);
                static void CPU(int &cores, int &sockets);
                static void NetworkUsage(long int &network);
                static const std::string& GetNodeName();

            protected:
            private:
        };
    }
}

#endif // SYSTEM_H
