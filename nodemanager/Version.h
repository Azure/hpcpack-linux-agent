#ifndef VERSION_H_INCLUDED
#define VERSION_H_INCLUDED

#include <string>

namespace hpc
{
    class Version
    {
        public:
            static const std::string& GetVersion()
            {
                static std::string version = "1.1.1.0";

                return version;
            }
    };
}

#endif // VERSION_H_INCLUDED
