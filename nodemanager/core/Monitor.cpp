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
#include "NodeManagerConfig.h"

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

    Logger::Info("Initializing GPU driver.");
    std::string output;
    this->gpuInitRet = System::ExecuteCommandOut(output, "nvidia-smi -pm 1");
    Logger::Info("Initialize GPU ret code {0}", this->gpuInitRet);

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
        return this->pagesPerSec;
    });

    this->collectors["\\Memory\\Available MBytes"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        return std::get<1>(this->metricData[3]);
    });

    this->collectors["\\System\\Context switches/sec"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        return this->contextSwitchesPerSec;
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
        if (instanceName != "_Total")
        {
            Logger::Warn("Unable to collect {0} for \\PhysicalDisk\\Disk Bytes/sec", instanceName);
        }

        return this->bytesPerSecond;
    });

    this->collectors["\\LogicalDisk\\Avg. Disk Queue Length"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
    {
        if (instanceName != "_Total")
        {
            Logger::Warn("Unable to collect {0} for \\LogicalDisk\\Avg. Disk Queue Length", instanceName);
        }

        return this->queueLength;
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
            return this->freeSpacePercent;
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

    if (this->gpuInitRet == 0)
    {
        this->collectors["\\GPU\\GPU Time (%)"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
        {
            if (instanceName == "_Total" || instanceName.empty())
            {
                return this->gpuInfo.GetGpuUtilization();
            }
            else
            {
                auto index = String::ConvertTo<size_t>(instanceName);
                if (index >= 0 && index < this->gpuInfo.GpuInfos.size())
                {
                    float v = this->gpuInfo.GpuInfos[index].GpuUtilization;
                    //Logger::Debug("\\GPU\\GPU Time (%), for index {0} is {1}", index, v);
                    return v;
                }
                else
                {
                    Logger::Warn("Collect \\GPU\\GPU Time (%) for instance {0}, index {1}, invalid index", instanceName, index);
                    return 0.0f;
                }
            }
        },
        [this]()
        {
            return this->gpuInfo.GetGpuInstanceNames();
        });

        this->collectors["\\GPU\\GPU Fan Speed (%)"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
        {
            if (instanceName == "_Total" || instanceName.empty())
            {
                return this->gpuInfo.GetFanPercentage();
            }
            else
            {
                auto index = String::ConvertTo<size_t>(instanceName);
                if (index >= 0 && index < this->gpuInfo.GpuInfos.size())
                {
                    return this->gpuInfo.GpuInfos[index].FanPercentage;
                }
                else
                {
                    Logger::Warn("Collect \\GPU\\GPU Fan Speed (%) for instance {0}, index {1}, invalid index", instanceName, index);
                    return 0.0f;
                }
            }
        },
        [this]()
        {
            return this->gpuInfo.GetGpuInstanceNames();
        });

        this->collectors["\\GPU\\GPU Memory Usage (%)"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
        {
            if (instanceName == "_Total" || instanceName.empty())
            {
                return this->gpuInfo.GetUsedMemoryPercentage();
            }
            else
            {
                auto index = String::ConvertTo<size_t>(instanceName);
                if (index >= 0 && index < this->gpuInfo.GpuInfos.size())
                {
                    return this->gpuInfo.GpuInfos[index].GetUsedMemoryPercentage();
                }
                else
                {
                    Logger::Warn("Collect \\GPU\\GPU Memory Usage (%) for instance {0}, index {1}, invalid index", instanceName, index);
                    return 0.0f;
                }
            }
        },
        [this]()
        {
            return this->gpuInfo.GetGpuInstanceNames();
        });

        this->collectors["\\GPU\\GPU Memory Used (MB)"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
        {
            if (instanceName == "_Total" || instanceName.empty())
            {
                // Get GPU Time;
                return this->gpuInfo.GetUsedMemoryMB();
            }
            else
            {
                auto index = String::ConvertTo<size_t>(instanceName);
                if (index >= 0 && index < this->gpuInfo.GpuInfos.size())
                {
                    return this->gpuInfo.GpuInfos[index].UsedMemoryMB;
                }
                else
                {
                    Logger::Warn("Collect \\GPU\\GPU Memory Used (MB) for instance {0}, index {1}, invalid index", instanceName, index);
                    return 0.0f;
                }
            }
        },
        [this]()
        {
            return this->gpuInfo.GetGpuInstanceNames();
        });

        this->collectors["\\GPU\\GPU Power Usage (Watts)"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
        {
            if (instanceName == "_Total" || instanceName.empty())
            {
                return this->gpuInfo.GetPowerWatt();
            }
            else
            {
                auto index = String::ConvertTo<size_t>(instanceName);
                if (index >= 0 && index < this->gpuInfo.GpuInfos.size())
                {
                    return this->gpuInfo.GpuInfos[index].PowerWatt;
                }
                else
                {
                    Logger::Warn("Collect \\GPU\\GPU Power Usage (Watts) for instance {0}, index {1}, invalid index", instanceName, index);
                    return 0.0f;
                }
            }
        },
        [this]()
        {
            return this->gpuInfo.GetGpuInstanceNames();
        });

        this->collectors["\\GPU\\GPU SM Clock (MHz)"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
        {
            if (instanceName == "_Total" || instanceName.empty())
            {
                // Get GPU Time;
                return this->gpuInfo.GetCurrentSMClock();
            }
            else
            {
                auto index = String::ConvertTo<size_t>(instanceName);
                if (index >= 0 && index < this->gpuInfo.GpuInfos.size())
                {
                    return this->gpuInfo.GpuInfos[index].CurrentSMClock;
                }
                else
                {
                    Logger::Warn("Collect \\GPU\\GPU SM Clock (MHz) for instance {0}, index {1}, invalid index", instanceName, index);
                    return 0.0f;
                }
            }
        },
        [this]()
        {
            return this->gpuInfo.GetGpuInstanceNames();
        });

        this->collectors["\\GPU\\GPU Temperature (degrees C)"] = std::make_shared<MetricCollectorBase>([this] (const std::string& instanceName)
        {
            if (instanceName == "_Total" || instanceName.empty())
            {
                // Get GPU Time;
                return this->gpuInfo.GetTemperature();
            }
            else
            {
                auto index = String::ConvertTo<size_t>(instanceName);
                if (index >= 0 && index < this->gpuInfo.GpuInfos.size())
                {
                    return this->gpuInfo.GpuInfos[index].Temperature;
                }
                else
                {
                    Logger::Warn("Collect \\GPU\\GPU Temperature for instance {0}, index {1}, invalid index", instanceName, index);
                    return 0.0f;
                }
            }
        },
        [this]()
        {
            return this->gpuInfo.GetGpuInstanceNames();
        });
    }

    auto metaDataUri = "http://169.254.169.254/metadata/instance?api-version=2017-08-01";
    this->metaDataClient = std::make_shared<web::http::client::http_client>(metaDataUri);
    this->metaDataRequest = std::make_shared<web::http::http_request>(web::http::methods::GET);
    this->metaDataRequest->headers().add("metadata", "true");
    this->QueryAzureInstanceMetadata();

    int result = pthread_create(&this->threadId, nullptr, MonitoringThread, this);
    if (result != 0) Logger::Error("Create monitoring thread result {0}, errno {1}", result, errno);
}

Monitor::~Monitor()
{
    if (this->threadId != 0)
    {
        // todo: graceful exit the thread.
        pthread_cancel(this->threadId);
        pthread_join(this->threadId, nullptr);
    }

    pthread_rwlock_destroy(&this->lock);
}

void Monitor::SetNodeUuid(const uuid& id)
{
    this->packet.Uuid.AssignFrom(id);
}

void Monitor::ApplyMetricConfig(MetricCountersConfig&& config, pplx::cancellation_token token)
{
    WriterLock writerLock(&this->lock);

    for_each(this->collectors.begin(), this->collectors.end(), [] (auto& kvp) { kvp.second->Reset(); });

    for (auto& counter : config.MetricCounters)
    {
        if (!this->EnableMetricCounter(counter, token))
        {
            Logger::Debug("Disabled counter MetricId {0}, InstanceId {1}, InstanceName {2} Path {3}", counter.MetricId, counter.InstanceId, counter.InstanceName, counter.Path);
        }
        else
        {
            Logger::Debug("Enabled counter MetricId {0}, InstanceId {1}, InstanceName {2} Path {3}", counter.MetricId, counter.InstanceId, counter.InstanceName, counter.Path);
        }
    }
}

bool Monitor::EnableMetricCounter(const MetricCounter& counterConfig, pplx::cancellation_token token)
{
    auto collector = this->collectors.find(counterConfig.Path);
    if (collector != this->collectors.end())
    {
        collector->second->ApplyConfig(counterConfig, token);
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
       // this->packet.Count = std::count_if(this->collectors.begin(), this->collectors.end(), [] (auto& kvp) { return kvp.second->IsEnabled(); });
        this->packet.TickCount = this->intervalSeconds;
        for (int i = 0; i < MaxCountersInPacket; i++)
        {
            this->packet.Umids[i] = Umid(0, 0);
            this->packet.Values[i] = 0.0f;
        }

        int p = 0;

        if (NodeManagerConfig::GetDebug())
        {
            Logger::Debug("Start get package data");
        }

        for (auto& c : this->collectors)
        {
            if (c.second->IsEnabled())
            {
                auto values = c.second->CollectValues();

                for (auto& v : values)
                {
                    if (NodeManagerConfig::GetDebug())
                    {
                        Logger::Debug("Report p={0}, value={1}, metricId={2}, instanceId={3}", p, v.first, v.second.MetricId, v.second.InstanceId);
                    }

                    this->packet.Umids[p] = v.second;
                    this->packet.Values[p] = v.first;
                    p ++;
                }
            }
        }

        this->packet.Count = p;

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

    std::vector<json::value> gpuValues;

    for (const auto& info : this->gpuInfo.GpuInfos)
    {
        json::value v;
        v["Name"] = json::value::string(info.Name);
        v["Uuid"] = json::value::string(info.Uuid);
        v["PciBusDevice"] = json::value::string(info.GetPciBusDevice());
        v["PciBusId"] = json::value::string(info.PciBusId);
        v["TotalMemory"] = info.TotalMemoryMB;
        v["MaxSMClock"] = info.MaxSMClock;

        gpuValues.push_back(v);
    }

    j["GpuInfo"] = json::value::array(gpuValues);

    if (this->remainingRequestCount == -1)
    {
        j["AzureInstanceMetadata"] = json::value::string(this->azureInstanceMetadata);
    }

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

        float freeSpacePercent = 0.0f, queueLength = 0.0f, pagesPerSec = 0.0f, contextSwitchesPerSec = 0.0f, bytesPerSecond = 0.0f;
        System::FreeSpace(freeSpacePercent);
        System::IostatX(queueLength);
        System::Vmstat(pagesPerSec, contextSwitchesPerSec);
        System::Iostat(bytesPerSecond);

        uint64_t networkCurrent = 0;
        int ret = System::NetworkUsage(networkCurrent, this->networkName);

        if (ret != 0)
        {
            Logger::Error("Error occurred while collecting network usage {0}", ret);
        }

        float networkUsage = (float)(networkCurrent - networkLast) / this->intervalSeconds;
        networkLast = networkCurrent;

        // ip address;
        std::string ipAddress = System::GetIpAddress(IpAddressVersion::V4, this->networkName);

        // cpu type;
        int cores, sockets;
        System::CPU(cores, sockets);

        // distro;
        const std::string& distro = System::GetDistroInfo();

        // networks;
        auto netInfo = System::GetNetworkInfo();

        // GPU
        System::GpuInfoList gpuInfo;
        if (this->gpuInitRet == 0)
        {
            this->gpuInitRet = System::QueryGpuInfo(gpuInfo);
        }

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

            this->freeSpacePercent = freeSpacePercent;
            this->queueLength = queueLength;
            this->pagesPerSec = pagesPerSec;
            this->contextSwitchesPerSec = contextSwitchesPerSec;
            this->bytesPerSecond = bytesPerSecond;

            if (this->gpuInitRet == 0)
            {
                Logger::Debug("Saving Gpu Info ret {0}, info count {1}", this->gpuInitRet, gpuInfo.GpuInfos.size());
                this->gpuInfo = std::move(gpuInfo);
            }
        }

        this->isCollected = true;

        sleep(this->intervalSeconds);
    }
}

void Monitor::QueryAzureInstanceMetadata()
{
    metaDataClient->request(*metaDataRequest).then([this](pplx::task<web::http::http_response> t)
    {
        try
        {
            auto response = t.get();
            if (response.status_code() == web::http::status_codes::OK)
            {
                this->azureInstanceMetadata = response.extract_string().get();
                Logger::Info("Get metadata of Azure instance. {0}", this->azureInstanceMetadata);
                this->remainingRequestCount = -1;
                return;
            }
        }
        catch (const std::exception& ex)
        {
            Logger::Warn("Exception when querying Azure node metadata. {0}.", ex.what());
        }

        Logger::Warn("Failed to get metadata of Azure instance. Remaining retry count = {0}.", --this->remainingRequestCount);
        if (this->remainingRequestCount != 0)
        {
            this->QueryAzureInstanceMetadata();
        }
    });
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
