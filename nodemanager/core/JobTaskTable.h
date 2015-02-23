#ifndef JOBTASKTABLE_H
#define JOBTASKTABLE_H

#include <map>
#include <cpprest/json.h>

#include "../data/TaskInfo.h"
#include "../data/JobInfo.h"
#include "../data/NodeInfo.h"

namespace hpc
{
    namespace core
    {
        class JobTaskTable
        {
            public:
                JobTaskTable() { }

                web::json::value GetNodeJson() const;
                web::json::value GetMetricJson() const;
                web::json::value GetTaskJson(int jobId, int taskId) const;

                void AddJobAndTask(int jobId, int taskId, hpc::data::TaskInfo&& taskInfo);
                hpc::data::JobInfo RemoveJob(int jobId);
                hpc::data::TaskInfo RemoveTask(int jobId, int taskId);

            protected:
            private:

                hpc::data::NodeInfo nodeInfo;
        };
    }
}

#endif // JOBTASKTABLE_H
