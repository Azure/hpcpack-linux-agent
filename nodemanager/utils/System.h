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
                static void CPUUsage(uint64_t &total, uint64_t &idle);
                static void Memory(uint64_t &available, uint64_t &total);
                static void CPU(int &cores, int &sockets);
                static int NetworkUsage(uint64_t &network, const std::string& netName);
                static int Vmstat(float &pagesPerSec, float &contextSwitchesPerSec);
                static int Iostat(float &bytesPerSec);
                static int IostatX(float &queueLength);
                static int FreeSpace(float &freeSpaceKB);
                static const std::string& GetNodeName();
                static bool IsCGroupInstalled();

                static const std::string& GetDistroInfo();

                static int CreateUser(
                    const std::string& userName,
                    const std::string& password);

                static int AddSshKey(
                    const std::string& userName,
                    const std::string& userHomeDir,
                    const std::string& key,
                    const std::string& fileName);

                static int RemoveSshKey(
                    const std::string& userHomeDir,
                    const std::string& fileName);

                static int AddAuthorizedKey(
                    const std::string& userHomeDir,
                    const std::string& key);

                static int RemoveAuthorizedKey(
                    const std::string& userHomeDir,
                    const std::string& key);

                static int DeleteUser(const std::string& userName);

                static int ResolveUserName(const std::string& domainUserName, std::string& output);

                static int TouchHomeDir(const std::string& userName, const std::string& umask);

                static int GetHomeDir(const std::string& userName, std::string& output);

                template <typename ... Args>
                static int ExecuteCommandIn(const std::string& input, const std::string& cmd, const Args& ... args)
                {
                    std::string command = String::Join(" ", cmd, args...);
                    //Logger::Debug("Executing cmd: {0}", command);
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
                    //Logger::Debug("Executing cmd: {0}", command);
                    int exitCode = (int)hpc::common::ErrorCodes::PopenError;

                    std::ostringstream result;
                    FILE* stream = popen(command.c_str(), "r");

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
                        int err = errno;
                        Logger::Error("Error when popen {0}, errno {1}", command, err);
                        result << "error when popen " << command << ", errno " << err << std::endl;
                    }

                    output = result.str();

                    if (exitCode != 0)
                    {
                        Logger::Warn("Executing {0}, error code {1}", command, exitCode);
                    }

                    return exitCode;
                }

            protected:
            private:
        };
    }
}

#endif // SYSTEM_H
