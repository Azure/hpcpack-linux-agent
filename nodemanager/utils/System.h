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

            protected:
            private:
        };
    }
}

#endif // SYSTEM_H
