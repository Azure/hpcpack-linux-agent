#include <string>
#include <sys/types.h>
#include <ifaddrs.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <iostream>
#include <fstream>
#include <unistd.h>
#include <set>

#include "System.h"
#include "String.h"
#include "Logger.h"

using namespace hpc::utils;

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

void System::CPUUsage(long int &total, long int &idle)
{
    std::ifstream fs("/proc/stat", std::ios::in);
    std::string cpu;
    long int user, nice, sys, iowait, irq, softirq;

    fs >> cpu >> user >> nice >> sys >> idle >> iowait >> irq >> softirq;
    fs.close();

    total = user + nice + sys + idle + iowait + irq + softirq;
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
        if (tokens[0] == "physical id")
        {
            physicalIds.insert(tokens[2]);
        }
        else if (tokens[0] == "core id")
        {
            coreIds.insert(tokens[2]);
        }
    }

    cores = coreIds.size();
    sockets = physicalIds.size();

    fs.close();
}

void System::NetworkUsage(long int &network)
{
    std::ifstream fs("/proc/meminfo", std::ios::in);
    std::string name;
    int receive, send;

    fs.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
    fs.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
    fs >> name >> receive >> send;
    network = receive + send;

    fs.close();
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
            exit(-1);
        }

        nodeName = buffer;
    }

    return nodeName;
}
