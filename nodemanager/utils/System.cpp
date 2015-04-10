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

            if (std::string(i->ifa_name) != name) continue;

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

void System::CPUUsage(long int &total, long int &idle)
{
    try
    {
        std::ifstream fs("/proc/stat", std::ios::in);
        std::string cpu;
        long int user, nice, sys, iowait, irq, softirq;

        fs >> cpu >> user >> nice >> sys >> idle >> iowait >> irq >> softirq;
        fs.close();

        total = user + nice + sys + idle + iowait + irq + softirq;
    }
    catch (std::exception& ex)
    {
        Logger::Error("CPUUsage get exception {0}", ex.what());
    }
}

void System::Memory(unsigned long &availableKb, unsigned long &totalKb)
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

   // Logger::Debug("Detected core count {0}, socket count {1}", cores, sockets);

    fs.close();
}

int System::NetworkUsage(long int &network, const std::string& netName)
{
    int ret = 1;
    std::ifstream fs("/proc/net/dev", std::ios::in);
    std::string name;
    int receive, send;

    fs.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
    fs.ignore(std::numeric_limits<std::streamsize>::max(), '\n');

    std::string tmp = netName + ":";
    while (name != tmp && fs.good())
    {
        fs >> name >> receive >> send;
        fs.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
    }

    if (name == tmp)
    {
        network = receive + send;
        ret = 0;
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

int System::CreateUser(const std::string& userName, const std::string& password)
{
    std::string output;

    int ret = System::ExecuteCommandOut(output, "useradd", userName, "-m");
    if (ret != 0)
    {
        Logger::Error("useradd {0} -m error code {1}", userName, ret);
        return ret;
    }

    std::string input = String::Join("", password, "\n", password, "\n");
    ret = System::ExecuteCommandIn(input, "passwd", userName);
    if (ret != 0)
    {
        Logger::Error("passwd {0} error code {1}", userName, ret);
    }

    return ret;
}

int System::DeleteUser(const std::string& userName)
{
    std::string output;

    int ret = System::ExecuteCommandOut(output, "userdel", userName, "-r");
    if (ret != 0)
    {
        Logger::Error("userdel {0} error code {1}", userName, ret);
    }

    return ret;
}
