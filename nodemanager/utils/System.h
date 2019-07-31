#ifndef SYSTEM_H
#define SYSTEM_H

#include <string>
#include <map>

#include "String.h"
#include "Logger.h"
#include "../common/ErrorCodes.h"
#include "Enumerable.h"

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

                // name, uuid, pci.bus_id, pci.device_id, total_memory, max_sm_clock, gpu %, fan %, memory %, used mem MB, power Watt, SM Clock MH, temperature C
                typedef struct _GpuInfo
                {
                    std::string Name;
                    std::string Uuid;
                    std::string PciBusId;
                    std::string DeviceId;
                    float TotalMemoryMB;
                    float MaxSMClock;
                    float FanPercentage;
                    float UsedMemoryMB;
                    float PowerWatt;
                    float CurrentSMClock;
                    float Temperature;
                    float GpuUtilization;

                    // take 00:01 from B174:00:01.0
                    std::string GetPciBusDevice() const
                    {
                        auto tokens = String::Split(this->PciBusId, '.');
                        auto ids = String::Split(tokens[0], ':');
                        return String::Join(':', ids[1], ids[2]);
                    }

                    float GetUsedMemoryPercentage() const { return 100 * this->UsedMemoryMB / this->TotalMemoryMB; }
                } GpuInfo;

                typedef struct _GpuInfoList
                {
                    std::vector<GpuInfo> GpuInfos;
                    float GetTotalMemoryMB() const { return Enumerable::Sum<std::vector<GpuInfo>, float, GpuInfo>(this->GpuInfos, [] (const GpuInfo& i) { return i.TotalMemoryMB; }); }
                    float GetMaxSMClock() const { return Enumerable::Avg<std::vector<GpuInfo>, float, GpuInfo>(this->GpuInfos, [] (const GpuInfo& i) { return i.MaxSMClock; }); }
                    float GetFanPercentage() const { return Enumerable::First<std::vector<GpuInfo>, float, GpuInfo>(this->GpuInfos, [] (const GpuInfo& i) { return i.FanPercentage; }); }
                    float GetUsedMemoryMB() const { return Enumerable::Sum<std::vector<GpuInfo>, float, GpuInfo>(this->GpuInfos, [] (const GpuInfo& i) { return i.UsedMemoryMB; }); }
                    float GetPowerWatt() const { return Enumerable::Sum<std::vector<GpuInfo>, float, GpuInfo>(this->GpuInfos, [] (const GpuInfo& i) { return i.PowerWatt; }); }
                    float GetCurrentSMClock() const { return Enumerable::Avg<std::vector<GpuInfo>, float, GpuInfo>(this->GpuInfos, [] (const GpuInfo& i) { return i.CurrentSMClock; }); }
                    float GetTemperature() const { return Enumerable::Avg<std::vector<GpuInfo>, float, GpuInfo>(this->GpuInfos, [] (const GpuInfo& i) { return i.Temperature; }); }

                    float GetGpuUtilization() const { return Enumerable::Avg<std::vector<GpuInfo>, float, GpuInfo>(this->GpuInfos, [] (const GpuInfo& i) { return i.GpuUtilization; }); }
                    float GetUsedMemoryPercentage() const { return 100 * this->GetUsedMemoryMB() / this->GetTotalMemoryMB(); }

                    std::vector<std::string> gpuInstanceNames;

                    std::vector<std::string> GetGpuInstanceNames()
                    {
                        if (this->gpuInstanceNames.size() != this->GpuInfos.size())
                        {
                            this->gpuInstanceNames.clear();
                            for (size_t i = 0; i < this->GpuInfos.size(); i++)
                            {
                                this->gpuInstanceNames.push_back(std::to_string(i));
                            }

                           // this->gpuInstanceNames.push_back("_Total");
                        }

                        return this->gpuInstanceNames;
                    }
                } GpuInfoList;

                static std::vector<NetInfo> GetNetworkInfo();
                static std::vector<std::string> GetIbDevices();
                static std::string GetIpAddress(IpAddressVersion version, const std::string& name);
                static void CPUUsage(uint64_t &total, uint64_t &idle);
                static void Memory(uint64_t &available, uint64_t &total);
                static void CPU(int &cores, int &sockets);
                static std::map<std::string, uint64_t> GetNetworkUsage();
                static void IbNetworkUsage(std::map<std::string, uint64_t> & networkUsage, bool logFailure = false);
                static int Vmstat(float &pagesPerSec, float &contextSwitchesPerSec);
                static int Iostat(float &bytesPerSec);
                static int IostatX(float &queueLength);
                static int FreeSpace(float &freeSpaceKB);
                static const std::string& GetNodeName();
                static bool IsCGroupInstalled();

                static const std::string& GetDistroInfo();

                static int CreateUser(
                    const std::string& userName,
                    const std::string& password,
                    bool isAdmin);

                static int AddSshKey(
                    const std::string& userName,
                    const std::string& key,
                    const std::string& fileName,
                    const std::string& filePermission,
                    std::string& filePath);

                static int RemoveSshKey(
                    const std::string& userName,
                    const std::string& fileName);

                static int AddAuthorizedKey(
                    const std::string& userName,
                    const std::string& key,
                    const std::string& filePermission,
                    std::string& filePath);

                static int RemoveAuthorizedKey(
                    const std::string& userName,
                    const std::string& key);

                static int GetHomeDir(const std::string& userName, std::string& homeDir);

                static int DeleteUser(const std::string& userName);
                static int CreateTempFolder(char* folderTemplate, const std::string& userName);
                static int WriteStringToFile(const std::string& fileName, const std::string& contents);

                static int QueryGpuInfo(GpuInfoList& gpuInfo);

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
