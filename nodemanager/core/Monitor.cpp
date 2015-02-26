#include <pthread.h>

#include "Monitor.h"
#include "../utils/ReaderLock.h"
#include "../utils/WriterLock.h"
#include "../utils/Logger.h"
#include "../utils/System.h"

using namespace hpc::core;
using namespace hpc::utils;

Monitor::Monitor(const std::string& nodeName) : Name(nodeName), lock(PTHREAD_RWLOCK_INITIALIZER)
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

json::value Monitor::ToJson()
{
    ReaderLock lock(&this->lock);
    json::value j;
    j["Name"] = json::value::string(this->Name);
    j["Time"] = json::value::string(this->Time);

    std::vector<json::value> umids;
    std::transform(this->metricData.cbegin(), this->metricData.cend(), std::back_inserter(umids), [](auto i)
    {
        json::value obj;
        obj["MetricId"] = i.first;
        obj["InstanceId"] = std::get<0>(i.second);
        return std::move(obj);
    });

    std::vector<json::value> values;
    std::transform(this->metricData.cbegin(), this->metricData.cend(), std::back_inserter(umids), [](auto i) { return std::get<1>(i.second); });

    j["Umids"] = json::value::array(umids);
    j["Values"] = json::value::array(values);
    j["TickCount"] = this->TickCount;

    return std::move(j);
}

void Monitor::Run()
{
    long int cpuLast = 0, idleLast = 0, networkLast = 0;

    while (true)
    {
        long int cpuCurrent = 0, idleCurrent = 0;

        System::CPUUsage(cpuCurrent, idleCurrent);
        long int totalDiff = cpuCurrent - cpuLast;
        long int idleDiff = idleCurrent - idleLast;
        float cpuUsage = (float)(100.0f * (totalDiff - idleDiff) / totalDiff);

        long int memory;
        System::AvailableMemory(memory);
        float availableMemory = (float)memory / 1024.0f;

        long int networkCurrent = 0;
        System::NetworkUsage(networkCurrent);
        float networkUsage = (float)(networkCurrent - networkLast) / this->intervalSeconds;

        {
            WriterLock writerLock(&this->lock);
            std::get<1>(this->metricData[1]) = cpuUsage;
            std::get<1>(this->metricData[3]) = availableMemory;
            std::get<1>(this->metricData[12]) = networkUsage;
        }

        sleep(this->intervalSeconds);
    }
}

void* Monitor::MonitoringThread(void* arg)
{
    pthread_setcancelstate(PTHREAD_CANCEL_ENABLE, nullptr);
    pthread_setcanceltype(PTHREAD_CANCEL_ASYNCHRONOUS, nullptr);

    Monitor* m = static_cast<Monitor*>(arg);
    m->Run();

    pthread_exit(nullptr);
}
