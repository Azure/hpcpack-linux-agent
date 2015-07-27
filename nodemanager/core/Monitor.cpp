#include <pthread.h>

#include "Monitor.h"
#include "../utils/ReaderLock.h"
#include "../utils/WriterLock.h"
#include "../utils/Logger.h"
#include "../utils/System.h"

using namespace hpc::core;
using namespace hpc::utils;
using namespace hpc::data;

Monitor::Monitor(const std::string& nodeName, const std::string& netName, int interval)
    : name(nodeName), networkName(netName), lock(PTHREAD_RWLOCK_INITIALIZER), intervalSeconds(interval),
    isCollected(false)
{
    std::get<0>(this->metricData[1]) = 1;
    std::get<0>(this->metricData[3]) = 0;
    std::get<0>(this->metricData[12]) = 1;

    int result = pthread_create(&this->threadId, nullptr, MonitoringThread, this);
    if (result != 0) Logger::Error("Create monitoring thread result {0}, errno {1}", result, errno);
}

Monitor::~Monitor()
{
    if (this->threadId != 0)
    {
        pthread_cancel(this->threadId);
        pthread_join(this->threadId, nullptr);
    }

    pthread_rwlock_destroy(&this->lock);
}

void Monitor::SetNodeUuid(const uuid& id)
{
    this->packet.Uuid.AssignFrom(id);
}

void Monitor::ApplyMetricConfig(const MetricCounterConfig& config)
{
    for (auto& counter : config.MetricCounters)
    {
        if (!this->EnableMetricCounter(counter))
        {
            Logger::Debug("Unable to enable metric counter MetricId: {0}, InstanceId: {1}, Path: {2}", counter.MetricId, counter.InstanceId, counter.Path);
        }
    }
}

bool Monitor::EnableMetricCounter(const MetricCounter& counterConfig)
{
    return false;
}

std::vector<unsigned char> Monitor::GetMonitorPacketData()
{
    const size_t MaxPacketSize = 1024;
    std::vector<unsigned char> packetData(MaxPacketSize);

    ReaderLock readerLock(&this->lock);

    if (this->isCollected)
    {
        // TODO: make the monitoring data configurable.
        this->packet.Count = 3;
        this->packet.TickCount = this->intervalSeconds;

        for (int i = 0; i < MaxCountersInPacket; i++)
        {
            this->packet.Umids[i] = Umid(0, 0);
            this->packet.Values[i] = 0.0f;
        }

        std::transform(this->metricData.cbegin(), this->metricData.cend(), this->packet.Umids, [] (auto i)
        {
            return Umid(i.first, std::get<0>(i.second));
        });

        std::transform(this->metricData.cbegin(), this->metricData.cend(), this->packet.Values, [] (auto i)
        {
            return std::get<1>(i.second);
        });

  //      Logger::Debug("GetMonitorPacketData, count {0}, TickCount {1}", this->packet.Count, this->packet.TickCount);

        memcpy(&packetData[0], &this->packet, std::min(sizeof(this->packet), MaxPacketSize));
    }

    return std::move(packetData);
}

json::value Monitor::GetRegisterInfo()
{
    ReaderLock lock(&this->lock);

    if (!this->isCollected)
    {
        return json::value::null();
    }

    json::value j;
    j["NodeName"] = json::value::string(this->name);
    j["Time"] = json::value::string(this->metricTime);

    j["IpAddress"] = json::value::string(this->ipAddress);
    j["CoreCount"] = this->coreCount;
    j["SocketCount"] = this->socketCount;
    j["MemoryMegabytes"] = this->totalMemoryMb;
    j["DistroInfo"] = json::value::string(this->distroInfo);

    std::vector<json::value> networkValues;

    for (const auto& info : this->networkInfo)
    {
        json::value v;
        v["Name"] = json::value::string(std::get<0>(info));
        v["MacAddress"] = json::value::string(std::get<1>(info));
        v["IpV4"] = json::value::string(std::get<2>(info));
        v["IpV6"] = json::value::string(std::get<3>(info));
        v["IsIB"] = std::get<4>(info);

        networkValues.push_back(v);
    }

    j["NetworksInfo"] = json::value::array(networkValues);

    return std::move(j);
}

void Monitor::Run()
{
    uint64_t cpuLast = 0, idleLast = 0, networkLast = 0;

    while (true)
    {
        time_t t;
        time(&t);

        uint64_t cpuCurrent = cpuLast + 1, idleCurrent = idleLast;

        System::CPUUsage(cpuCurrent, idleCurrent);
        uint64_t totalDiff = cpuCurrent - cpuLast;
        uint64_t idleDiff = idleCurrent - idleLast;
        float cpuUsage = (float)(100.0f * (totalDiff - idleDiff) / totalDiff);
        cpuLast = cpuCurrent;
        idleLast = idleCurrent;

        uint64_t available, total;
        System::Memory(available, total);
        float availableMemoryMb = (float)available / 1024.0f;
        float totalMemoryMb = (float)total / 1024.0f;

        uint64_t networkCurrent = 0;
        int ret = System::NetworkUsage(networkCurrent, "eth0");

        if (ret != 0)
        {
            Logger::Error("Error occurred while collecting network usage {0}", ret);
        }

        float networkUsage = (float)(networkCurrent - networkLast) / this->intervalSeconds;
        networkLast = networkCurrent;

        // ip address;
        std::string ipAddress = System::GetIpAddress(IpAddressVersion::V4, "eth0");

        // cpu type;
        int cores, sockets;
        System::CPU(cores, sockets);

        // distro;
        const std::string& distro = System::GetDistroInfo();

        // networks;
        auto netInfo = System::GetNetworkInfo();

        {
            WriterLock writerLock(&this->lock);

            this->metricTime = ctime(&t);

            std::get<1>(this->metricData[1]) = cpuUsage;
            std::get<1>(this->metricData[3]) = availableMemoryMb;
            std::get<1>(this->metricData[12]) = networkUsage;

            this->totalMemoryMb = totalMemoryMb;
            this->ipAddress = ipAddress;
            this->coreCount = cores;
            this->socketCount = sockets;
            this->distroInfo = distro;
            this->networkInfo = std::move(netInfo);
        }

        this->isCollected = true;

        sleep(this->intervalSeconds);
    }
}

void* Monitor::MonitoringThread(void* arg)
{
    pthread_setcancelstate(PTHREAD_CANCEL_ENABLE, nullptr);
    pthread_setcanceltype(PTHREAD_CANCEL_ASYNCHRONOUS, nullptr);

    Monitor* m = static_cast<Monitor*>(arg);
    Logger::Info("Monitoring thread created. Interval {0}", m->intervalSeconds);
    m->Run();

    pthread_exit(nullptr);
}
