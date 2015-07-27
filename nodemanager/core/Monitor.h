#ifndef MONITOR_H
#define MONITOR_H

#include <cpprest/json.h>
#include <map>
#include <boost/uuid/uuid.hpp>

#include "../utils/System.h"
#include "../data/MonitoringPacket.h"
#include "../arguments/MetricCounter.h"
#include "../arguments/MetricCountersConfig.h"

using namespace web;
using namespace boost::uuids;

namespace hpc
{
    namespace core
    {
        class Monitor
        {
            public:
                Monitor(
                    const std::string& nodeName,
                    const std::string& networkName,
                    int interval);

                ~Monitor();

                std::vector<unsigned char> GetMonitorPacketData();
                json::value GetRegisterInfo();

                void SetNodeUuid(const uuid& id);
                void ApplyMetricConfig(const MetricCountersConfig& config);

            protected:
            private:
                bool EnableMetricCounter(const MetricCounter& counterConfig);
                void Run();

                static void* MonitoringThread(void* arg);

                static const int MaxCountersInPacket = 80;

                std::string name;
                std::string networkName;
                std::string metricTime;
                std::map<int, std::tuple<int, float>> metricData;
                int coreCount;
                int socketCount;
                int totalMemoryMb;
                std::string ipAddress;
                std::string distroInfo;
                std::vector<hpc::utils::System::NetInfo> networkInfo;
                hpc::data::MonitoringPacket<MaxCountersInPacket> packet = 1;
                pthread_rwlock_t lock;

                int intervalSeconds;
                bool isCollected;
                pthread_t threadId = 0;
        };
    }
}

#endif // MONITOR_H
