#include <pthread.h>
#include <boost/range/algorithm.hpp>
#include <boost/range/adaptors.hpp>
#include <boost/phoenix.hpp>

#include "Monitor.h"
#include "../utils/ReaderLock.h"
#include "../utils/WriterLock.h"
#include "../utils/Logger.h"
#include "../utils/System.h"
#include "JobTaskTable.h"

using namespace hpc::core;
using namespace hpc::utils;
using namespace hpc::data;
using namespace hpc::arguments;
using namespace boost::phoenix::arg_names;

Monitor::Monitor(const std::string& nodeName, const std::string& netName, int interval)
    : name(nodeName), networkName(netName), lock(PTHREAD_RWLOCK_INITIALIZER), intervalSeconds(interval),
    isCollected(false)
{
    std::get<0>(this->metricData[1]) = 1;
    std::get<0>(this->metricData[3]) = 0;
    std::get<0>(this->metricData[12]) = 1;

    this->collectors["\\Processor\\% Processor Time"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        if (instanceName == "_Total")
        {
            return std::get<1>(this->metricData[1]);
        }
        else
        {
            Logger::Warn("Unable to collect {0} for \\Processor\\% Processor Time", instanceName);
            return 0.0f;
        }
    });

    this->collectors["\\Memory\\Pages/sec"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        float pagesPerSec = 0, contextSwitchesPerSec = 0;
        System::Vmstat(pagesPerSec, contextSwitchesPerSec);
        return (float)pagesPerSec;
    });

    this->collectors["\\Memory\\Available MBytes"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        return std::get<1>(this->metricData[3]);
    });

    this->collectors["\\System\\Context switches/sec"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        float pagesPerSec = 0, contextSwitchesPerSec = 0;
        System::Vmstat(pagesPerSec, contextSwitchesPerSec);
        return (float)contextSwitchesPerSec;
    });

    this->collectors["\\System\\System Calls/sec"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        if (NodeManagerConfig::GetDebug())
        {
            Logger::Warn("Unable to collect {0} for \\System\\System Calls/sec", instanceName);
        }

        return 0.0f;
    });

    this->collectors["\\PhysicalDisk\\Disk Bytes/sec"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        float bytesPerSecond = 0.0f;
        if (instanceName == "_Total")
        {
            System::Iostat(bytesPerSecond);
        }
        else
        {
            Logger::Warn("Unable to collect {0} for \\PhysicalDisk\\Disk Bytes/sec", instanceName);
        }

        return bytesPerSecond;
    });

    this->collectors["\\LogicalDisk\\Avg. Disk Queue Length"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        float queueLength = 0.0f;
        if (instanceName == "_Total")
        {
            System::IostatX(queueLength);
        }
        else
        {
            Logger::Warn("Unable to collect {0} for \\LogicalDisk\\Avg. Disk Queue Length", instanceName);
        }

        return queueLength;
    });

    this->collectors["\\Node Manager\\Number of Cores in use"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        float coresInUse = 0.0f;
        auto* table = JobTaskTable::GetInstance();
        if (table != nullptr)
        {
            coresInUse = table->GetCoresInUse();
        }

        return coresInUse;
    });

    this->collectors["\\Node Manager\\Number of Running Jobs"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        float runningJobs = 0.0f;
        auto* table = JobTaskTable::GetInstance();
        if (table != nullptr)
        {
            runningJobs = table->GetJobCount();
        }

        return runningJobs;
    });

    this->collectors["\\Node Manager\\Number of Running Tasks"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        float runningTasks = 0.0f;
        auto* table = JobTaskTable::GetInstance();
        if (table != nullptr)
        {
            runningTasks = table->GetTaskCount();
        }

        return runningTasks;
    });

    this->collectors["\\LogicalDisk\\% Free Space"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        if (instanceName == "_Total" || instanceName.empty())
        {
            float freeSpacePercent = 0.0f;
            System::FreeSpace(freeSpacePercent);
            return freeSpacePercent;
        }
        else
        {
            Logger::Warn("Unable to collect {0} for \\LogicalDisk\\% Free Space", instanceName);
            return 0.0f;
        }
    });

    this->collectors["\\Network Interface\\Bytes Total/sec"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        if (instanceName == "eth0" || instanceName.empty())
        {
            return std::get<1>(this->metricData[12]);
        }
        else
        {
            return 0.0f;
            Logger::Warn("Unable to collect {0} for \\Network Interface\\Bytes Total/sec", instanceName);
        }
    });

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

void Monitor::ApplyMetricConfig(MetricCountersConfig&& config)
{
    WriterLock writerLock(&this->lock);

    for_each(this->collectors.begin(), this->collectors.end(), [] (auto& kvp) { kvp.second->Reset(); });

    for (auto& counter : config.MetricCounters)
    {
        if (!this->EnableMetricCounter(counter))
        {
            Logger::Debug("Disabled counter MetricId {0}, InstanceId {1}, InstanceName {2} Path {3}", counter.MetricId, counter.InstanceId, counter.InstanceName, counter.Path);
        }
        else
        {
            Logger::Debug("Enabled counter MetricId {0}, InstanceId {1}, InstanceName {2} Path {3}", counter.MetricId, counter.InstanceId, counter.InstanceName, counter.Path);
        }
    }
}

bool Monitor::EnableMetricCounter(const MetricCounter& counterConfig)
{
    auto collector = this->collectors.find(counterConfig.Path);
    if (collector != this->collectors.end())
    {
        collector->second->ApplyConfig(counterConfig);
        return true;
    }

    return false;
}

std::vector<unsigned char> Monitor::GetMonitorPacketData()
{
    const size_t MaxPacketSize = 1024;
    std::vector<unsigned char> packetData(MaxPacketSize);

    ReaderLock readerLock(&this->lock);

    if (this->isCollected)
    {
        this->packet.Count = std::count_if(this->collectors.begin(), this->collectors.end(), [] (auto& kvp) { return kvp.second->IsEnabled(); });
        this->packet.TickCount = this->intervalSeconds;
        for (int i = 0; i < MaxCountersInPacket; i++)
        {
            this->packet.Umids[i] = Umid(0, 0);
            this->packet.Values[i] = 0.0f;
        }

        int p = 0;

        for (auto& c : this->collectors)
        {
            if (c.second->IsEnabled())
            {
                auto values = c.second->CollectValues();

                for (auto& v : values)
                {
                    if (NodeManagerConfig::GetDebug())
                    {
                        Logger::Debug("Report {0} {1} {2}", v.first, v.second.MetricId, v.second.InstanceId);
                    }

                    this->packet.Umids[p] = v.second;
                    this->packet.Values[p] = v.first;
                    p ++;
                }
            }
        }

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
