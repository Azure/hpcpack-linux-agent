#ifndef NODEINFO_H
#define NODEINFO_H

#include <cpprest/json.h>
#include <string>
#include <map>

#include "JobInfo.h"

namespace hpc
{
    namespace data
    {
        enum class NodeAvailability
        {
            AlwaysOn = 0,
            Available = 1,
            Occupied = 2
        };

        struct NodeInfo
        {
            public:
                NodeInfo();

                web::json::value ToJson();

                NodeAvailability Availability = NodeAvailability::AlwaysOn;
                bool JustStarted = true;
                std::string MacAddress;
                std::string Name;

                std::map<int, std::shared_ptr<JobInfo>> Jobs;
            protected:
            private:
        };
    }
}

#endif // NODEINFO_H
