#ifndef SYSTEM_H
#define SYSTEM_H

#include <string>

#include "String.h"

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
                static void NetworkUsage(long int &network, const std::string& netName);
                static const std::string& GetNodeName();
                static bool IsCGroupInstalled();

                template <typename ... Args>
                static int ExecuteCommand(std::string& output, const std::string& cmd, const Args& ... args)
                {
                    std::string command = String::Join(" ", cmd, args...);
                    FILE* stream = popen(command.c_str(), "r");

                    std::ostringstream result;
                    int exitCode = -1;

                    if (stream)
                    {
                        char buffer[512];
                        while (fgets(buffer, sizeof(buffer), stream) != nullptr)
                        {
                            result << buffer;
                        }

                        int ret = pclose(stream);
                        exitCode = WEXITSTATUS(ret);
                    }
                    else
                    {
                        result << "error when popen " << command << std::endl;
                    }

                    output = result.str();

                    return exitCode;
                }

            protected:
            private:
        };
    }
}

#endif // SYSTEM_H
