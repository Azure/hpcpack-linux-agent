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
                Monitor(const std::string& nodeName);
                ~Monitor();

                json::value ToJson();
            protected:
            private:
                void Run();
                static void* MonitoringThread(void* arg);

                std::string Name;
                std::string Time;
                int TickCount;
                std::map<int, std::tuple<int, float>> metricData;
                pthread_rwlock_t lock;

                int intervalSeconds;
                pthread_t threadId;
        };
    }
}

#endif // MONITOR_H
