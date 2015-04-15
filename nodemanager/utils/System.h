#ifndef SYSTEM_H
#define SYSTEM_H

#include <string>

#include "String.h"
#include "Logger.h"
#include "../common/ErrorCodes.h"

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
                typedef std::tuple<std::string, std::string, std::string, std::string, bool> NetInfo;

                static std::vector<NetInfo> GetNetworkInfo();
                static std::string GetIpAddress(IpAddressVersion version, const std::string& name);
                static void CPUUsage(long int &total, long int &idle);
                static void Memory(unsigned long &available, unsigned long &total);
                static void CPU(int &cores, int &sockets);
                static int NetworkUsage(long int &network, const std::string& netName);
                static const std::string& GetNodeName();
                static bool IsCGroupInstalled();

                static const std::string& GetDistroInfo();

                static int CreateUser(
                    const std::string& userName,
                    const std::string& password,
                    const std::string& privateKey,
                    const std::string& publicKey);

                static int DeleteUser(const std::string& userName);

                template <typename ... Args>
                static int ExecuteCommandIn(const std::string& input, const std::string& cmd, const Args& ... args)
                {
                    std::string command = String::Join(" ", cmd, args...);
                    Logger::Debug("Executing cmd: {0}", command);
                    FILE* stream = popen(command.c_str(), "w");
                    int exitCode = (int)hpc::common::ErrorCodes::PopenError;

                    if (stream)
                    {
                        if (!input.empty())
                        {
                            fputs(input.c_str(), stream);
                        }

                        int ret = pclose(stream);
                        exitCode = WEXITSTATUS(ret);
                    }
                    else
                    {
                        Logger::Error("Error when popen {0}", command);
                    }

                    return exitCode;
                }

                template <typename ... Args>
                static int ExecuteCommandOut(std::string& output, const std::string& cmd, const Args& ... args)
                {
                    std::string command = String::Join(" ", cmd, args...);
                    Logger::Debug("Executing cmd: {0}", command);
                    FILE* stream = popen(command.c_str(), "r");
                    int exitCode = (int)hpc::common::ErrorCodes::PopenError;

                    std::ostringstream result;

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
                        Logger::Error("Error when popen {0}", command);
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
