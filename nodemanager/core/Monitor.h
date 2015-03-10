#ifndef MONITOR_H
#define MONITOR_H

#include <cpprest/json.h>
#include <map>

using namespace web;

namespace hpc
{
    namespace core
    {
        class Monitor
        {
            public:
                Monitor(const std::string& nodeName, int interval);
                ~Monitor();

                json::value ToJson();
            protected:
            private:
                void Run();
                static void* MonitoringThread(void* arg);

                std::string name;
                std::string metricTime;
                std::map<int, std::tuple<int, float>> metricData;
                int coreCount;
                int socketCount;
                int totalMemoryMb;
                std::string ipAddress;
                pthread_rwlock_t lock;

                int intervalSeconds;
                pthread_t threadId;
        };
    }
}

#endif // MONITOR_H
