#include <string>
#include <sys/types.h>
#include <ifaddrs.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <iostream>
#include <sstream>
#include <fstream>
#include <unistd.h>
#include <set>

#include "System.h"
#include "String.h"
#include "Logger.h"
#include "../common/ErrorCodes.h"

using namespace hpc::utils;
using namespace hpc::common;

std::vector<System::NetInfo> System::GetNetworkInfo()
{
    std::vector<System::NetInfo> info;

    std::string rawData;

    int ret = System::ExecuteCommandOut(rawData, "ip", "addr");
    if (ret == 0)
    {
        std::string name, mac, ipV4, ipV6;
        bool isIB = false;

        std::istringstream iss(rawData);
        std::string line;
        while (getline(iss, line))
        {
            std::istringstream lineStream(line);
            if (line.empty()) continue;

            if (line[0] >= '0' && line[0] <= '9')
            {
                if (!name.empty())
                {
                    info.push_back(System::NetInfo(name, mac, ipV4, ipV6, isIB));
                    name.clear();
                    mac.clear();
                    ipV4.clear();
                    ipV6.clear();
                    isIB = false;
                }

                lineStream >> name >> name;

                // temporarily identify IB
                if (name == "eth1:")
                {
                    isIB = true;
                }

                continue;
            }

            std::string head, value;
            lineStream >> head >> value;

            if (head.compare(0, 4, "link") == 0)
            {
                mac = value;
            }
            else if (head == "inet")
            {
                ipV4 = value;
            }
            else if (head == "inet6")
            {
                ipV6 = value;
            }
        }

        if (!name.empty())
        {
            info.push_back(System::NetInfo(name, mac, ipV4, ipV6, isIB));
        }
    }

    return std::move(info);
}

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

            if ((name.empty() && std::string(i->ifa_name) == "lo:") ||
                std::string(i->ifa_name) != name)
            {
                continue;
            }

            const int BufferLength = isV4 ? INET_ADDRSTRLEN : INET6_ADDRSTRLEN;
            char buffer[BufferLength];
            inet_ntop(Family, tmp, buffer, BufferLength);

            ip = buffer;
       //     Logger::Debug("Found IPAddress {0}, name {1}", ip, name);
        }
    }

    if (ifAddr != nullptr) freeifaddrs(ifAddr);

    return std::move(ip);
}

void System::CPUUsage(uint64_t &total, uint64_t &idle)
{
    try
    {
        std::ifstream fs("/proc/stat", std::ios::in);
        std::string cpu;
        uint64_t user, nice, sys, iowait, irq, softirq;

        fs >> cpu >> user >> nice >> sys >> idle >> iowait >> irq >> softirq;
        fs.close();

        total = user + nice + sys + idle + iowait + irq + softirq;
    }
    catch (std::exception& ex)
    {
        Logger::Error("CPUUsage get exception {0}", ex.what());
    }
}

void System::Memory(uint64_t &availableKb, uint64_t &totalKb)
{
    std::ifstream fs("/proc/meminfo", std::ios::in);
    std::string name, unit;

    fs >> name >> totalKb >> unit;
    fs >> name >> availableKb >> unit;

    fs.close();
}

void System::CPU(int &cores, int &sockets)
{
    std::ifstream fs("/proc/cpuinfo", std::ios::in);
    std::string name, unit;

    std::set<std::string> physicalIds;
    std::set<std::string> coreIds;

    std::string line;
    while (getline(fs, line))
    {
        auto tokens = String::Split(line, ':');

        if (tokens.size() >= 2)
        {
            if (String::Trim(tokens[0]) == "physical id")
            {
                physicalIds.insert(tokens[1]);
            }
            else if (String::Trim(tokens[0]) == "processor")
            {
                coreIds.insert(tokens[1]);
            }
        }
    }

    cores = coreIds.size();
    sockets = physicalIds.size();
    sockets = (sockets > 0)? sockets : 1;

   // Logger::Debug("Detected core count {0}, socket count {1}", cores, sockets);

    fs.close();
}

int System::Vmstat(float &pagesPerSec, float &contextSwitchesPerSec)
{
    std::string output;

    int ret = System::ExecuteCommandOut(output, "vmstat");

    if (ret == 0)
    {
        std::istringstream iss(output);

        iss.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        iss.ignore(std::numeric_limits<std::streamsize>::max(), '\n');

        int r, b, swpd, free, buff, cache, si, so, bi, bo, in, cs, us, sy, id, wa, st;
        iss >> r >> b >> swpd >> free >> buff >> cache >> si >> so >> bi >> bo
            >> in >> cs >> us >> sy >> id >> wa >> st;

        pagesPerSec = si + so;
        contextSwitchesPerSec = cs;
    }

    return ret;
}

int System::Iostat(float &bytesPerSecond)
{
    std::string output;

    int ret = System::ExecuteCommandOut(output, "iostat -dk");

    if (ret == 0)
    {
        std::istringstream iss(output);

        std::string device;

        while (device != "Device:" && iss.good())
        {
            iss >> device;
            iss.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        }

        float tps, read, write;

        iss >> device >> tps >> read >> write;

        bytesPerSecond = (read + write) * 1024;
    }

    return ret;
}

int System::IostatX(float &queueLength)
{
    std::string output;

    int ret = System::ExecuteCommandOut(output, "iostat -x");

    if (ret == 0)
    {
        std::istringstream iss(output);

        std::string device;

        while (device != "Device:" && iss.good())
        {
            iss >> device;
            iss.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
        }

        float rrqm, wrqm, r, w, rkb, wkb, avgrq, avgqu;

        iss >> device >> rrqm >> wrqm >> r >> w >> rkb >> wkb >> avgrq >> avgqu;

        queueLength = avgqu;
    }

    return ret;
}

int System::FreeSpace(float &freeSpacePercent)
{
    std::string output;

    int ret = System::ExecuteCommandOut(output, "df -k");

    if (ret == 0)
    {
        std::istringstream iss(output);
        iss.ignore(std::numeric_limits<std::streamsize>::max(), '\n');

        std::string mountPoint;

        while (mountPoint != "/" && iss.good())
        {
            std::string tmp;
            iss >> tmp >> tmp >> tmp >> tmp >> freeSpacePercent >> tmp >> mountPoint;
        }

        if (mountPoint != "/")
        {
            ret = 1;
        }
        else
        {
            freeSpacePercent = 100 - freeSpacePercent;
        }
    }

    return ret;
}

int System::NetworkUsage(uint64_t &network, const std::string& netName)
{
    int ret = 1;
    std::ifstream fs("/proc/net/dev", std::ios::in);
    std::string name;
    uint64_t receive, send, tmp;

    fs.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
    fs.ignore(std::numeric_limits<std::streamsize>::max(), '\n');

    network = 0;

    while (fs.good())
    {
        std::getline(fs, name, ':');
        name = String::Trim(name);

        if (netName.empty() || netName == name)
        {
            fs >> receive >> tmp >> tmp >> tmp >> tmp >> tmp >> tmp >> tmp >> send;
            network += receive + send;
            ret = 0;
        }

        fs.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
    }

    fs.close();

    return ret;
}

const std::string& System::GetNodeName()
{
    static std::string nodeName;
    if (nodeName.empty())
    {
        char buffer[256];
        if (-1 == gethostname(buffer, 255))
        {
            Logger::Error("gethostname failed with errno {0}", errno);
            exit((int)ErrorCodes::GetHostNameError);
        }

        nodeName = buffer;
        std::transform(
            nodeName.begin(),
            nodeName.end(),
            nodeName.begin(),
            ::toupper);
    }

    return nodeName;
}

const std::string& System::GetDistroInfo()
{
    static std::string distroInfo;
    if (distroInfo.empty())
    {
        int ret = System::ExecuteCommandOut(distroInfo, "cat", "/proc/version");
        if (ret != 0)
        {
            Logger::Error("cat /proc/version error code {0} {1}", ret, distroInfo);
        }
    }

    return distroInfo;
}

int System::QueryGpuInfo(System::GpuInfoList& gpuInfo)
{
    std::string gpuInfoString;
    int ret = System::ExecuteCommandOut(
        gpuInfoString,
        "nvidia-smi",
        "--format=csv,noheader",
        "--query-gpu=name,uuid,pci.bus_id,pci.device_id,memory.total,clocks.max.sm,fan.speed,memory.used,power.draw,clocks.current.sm,temperature.gpu,utilization.gpu");

    if (127 == ret)
    {
        Logger::Warn("No nvidia-smi command found.");
        return ret;
    }

    std::vector<std::string> gpuStrings = String::Split(gpuInfoString, '\n');
    gpuInfo.GpuInfos.clear();
    for (auto s : gpuStrings)
    {
        auto values = String::Split(s, ',');
        System::GpuInfo info;
        info.Name = String::Trim(values[0]);
        info.Uuid = String::Trim(values[1]);
        info.PciBusId = String::Trim(values[2]);
        info.DeviceId = String::Trim(values[3]);
        info.TotalMemoryMB = String::ConvertTo<float>(values[4]);
        info.MaxSMClock = String::ConvertTo<float>(values[5]);
        info.FanPercentage = String::ConvertTo<float>(values[6]);
        info.UsedMemoryMB = String::ConvertTo<float>(values[7]);
        info.PowerWatt = String::ConvertTo<float>(values[8]);
        info.CurrentSMClock = String::ConvertTo<float>(values[9]);
        info.Temperature = String::ConvertTo<float>(values[10]);
        info.GpuUtilization = String::ConvertTo<float>(values[11]);

        gpuInfo.GpuInfos.push_back(info);
    }

    return ret;
}

int System::CreateUser(
    const std::string& userName,
    const std::string& password)
{
    std::string output;

    int ret = System::ExecuteCommandOut(output, "useradd", userName, "-m", "-s /bin/bash");
    if (ret != 0)
    {
        Logger::Warn("useradd {0} -m error code {1}", userName, ret);

        if (ret != 9) // exist
        {
            return ret;
        }
    }

    if (ret != 9)
    {
        std::string input = String::Join("", password, "\n", password, "\n");
        ret = System::ExecuteCommandIn(input, "passwd", userName);
        if (ret != 0)
        {
            Logger::Error("passwd {0} error code {1}", userName, ret);
            return ret;
        }
    }

    return ret;
}

int System::GetHomeDir(const std::string& userName, std::string& homeDir)
{
    std::string folder = String::Join("", "~", userName);

    int ret = System::ExecuteCommandOut(
        homeDir,
        "echo -n",
        folder);

    if (0 != ret || homeDir.find('~') != homeDir.npos)
    {
        Logger::Error("Cannot find home folder for user {0}", userName);
        return ret == 0 ? (int)ErrorCodes::CannotFindHomeDir : ret;
    }

    return ret;
}

int System::AddSshKey(
    const std::string& userName,
    const std::string& key,
    const std::string& fileName,
    const std::string& filePermission,
    std::string& filePath)
{
    std::string output;
    int ret = System::GetHomeDir(userName, output);

    if (0 != ret) { return ret; }

    std::string sshFolder = String::Join("", output, "/.ssh/");

    Logger::Debug("User {0}'s ssh folder {1}", userName, sshFolder);

    ret = System::ExecuteCommandOut(
        output,
        "[ -d ", sshFolder, " ] || mkdir -p ", sshFolder,
        " && chown ", userName, " ", sshFolder, " && chmod 700 ", sshFolder);

    if (ret != 0)
    {
        Logger::Info("Cannot create folder {0}, error code {1}", sshFolder, ret);
        return ret;
    }

    filePath = String::Join("", sshFolder, fileName);

    if (!key.empty())
    {
        std::ifstream test(filePath);
        // won't overwrite existing user's private key
        if (!test.good())
        {
            std::ofstream keyFile(filePath, std::ios::trunc);
            keyFile << key;
            keyFile.close();

            ret = System::ExecuteCommandOut(output, "chown", userName, filePath, "&& chmod", filePermission, filePath);

            if (0 != ret)
            {
                Logger::Error("Error when change the file {0}'s permission to {1}, ret {2}", filePath, filePermission, ret);
            }
        }
        else
        {
            Logger::Info("File {0} exist, skip overwriting", filePath);
        }

        test.close();

        return ret;
    }

    return -1;
}

int System::RemoveSshKey(
    const std::string& userName,
    const std::string& fileName)
{
    std::string output;
    int ret = System::GetHomeDir(userName, output);

    if (0 != ret) { return ret; }

    std::string sshFolder = String::Join("", output, "/.ssh/");

    auto keyFileName = String::Join("", sshFolder, fileName);
    std::ifstream test(keyFileName);
    // won't overwrite existing user's private key
    if (test.good())
    {
        std::string output;
        ret = System::ExecuteCommandOut(output, "rm -f", keyFileName);
    }

    test.close();

    return ret;
}

int System::AddAuthorizedKey(
    const std::string& userName,
    const std::string& key,
    const std::string& filePermission,
    std::string& filePath)
{
    std::string output;
    int ret = System::GetHomeDir(userName, output);

    if (0 != ret) { return ret; }

    std::string sshFolder = String::Join("", output, "/.ssh/");

    filePath = String::Join("", sshFolder, "authorized_keys");

    std::ofstream authFile(filePath, std::ios::app);

    ret = (int)ErrorCodes::WriteFileError;

    if (authFile.good())
    {
        authFile << key;

        ret = 0;
    }

    authFile.close();

    if (0 != ret)
    {
        Logger::Error("Error when open the auth file {0}", filePath);
        return ret;
    }

    ret = System::ExecuteCommandOut(output, "chown", userName, filePath, "&& chmod", filePermission, filePath);

    if (0 != ret)
    {
        Logger::Error("Error when change the file {0}'s permission to {1}, ret {2}", filePath, filePermission, ret);
    }

    return ret;
}

int System::RemoveAuthorizedKey(
    const std::string& userName,
    const std::string& key)
{
    std::string output;
    int ret = System::GetHomeDir(userName, output);

    if (0 != ret) { return ret; }

    std::string sshFolder = String::Join("", output, "/.ssh/");
    std::string authFileName = String::Join("", sshFolder, "authorized_keys");

    std::ifstream authFile(authFileName, std::ios::in);
    std::vector<std::string> lines;

    ret = (int)ErrorCodes::WriteFileError;

    if (!authFile.good())
    {
        authFile.close();
        return ret;
    }

    std::string trimKey = String::Trim(key);
    std::string line;
    while (getline(authFile, line))
    {
        if (String::Trim(line) != trimKey)
        {
            lines.push_back(line);
        }
    }

    authFile.close();

    std::ofstream authFileOut(authFileName, std::ios::trunc);
    for (auto& k : lines)
    {
        authFileOut << k << std::endl;
    }

    ret = 0;
    authFileOut.close();

    return ret;
}

int System::DeleteUser(const std::string& userName)
{
    std::string output;

    // kill all processes associated with this user.
    int ret = System::ExecuteCommandOut(output, "pkill -KILL -U `id -ur", userName, "`;",
        "userdel", userName, "-r -f");

    if (ret != 0)
    {
        Logger::Error("userdel {0} error code {1}", userName, ret);
    }

    return ret;
}

int System::CreateTempFolder(char* folderTemplate, const std::string& userName)
{
    char* p = mkdtemp(folderTemplate);

    if (p)
    {
        std::string output;
        int ret = System::ExecuteCommandOut(output, "chown -R", userName, p);
        if (ret == 0)
        {
            ret = System::ExecuteCommandOut(output, "chmod -R 700", p);
        }

        return ret;
    }
    else
    {
        return errno;
    }
}

int System::WriteStringToFile(const std::string& fileName, const std::string& contents)
{
    std::ofstream os(fileName, std::ios::trunc);
    if (!os)
    {
        return (int)ErrorCodes::WriteFileError;
    }

    os << contents;
    os.close();

    return 0;
}
