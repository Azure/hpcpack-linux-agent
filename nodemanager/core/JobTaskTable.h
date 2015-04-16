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
                JobTaskTable() : lock(PTHREAD_RWLOCK_INITIALIZER) { }
                ~JobTaskTable() { pthread_rwlock_destroy(&this->lock); }

                web::json::value ToJson();
             //   web::json::value GetTaskJson(int jobId, int taskId) const;

                std::shared_ptr<hpc::data::TaskInfo> AddJobAndTask(int jobId, int taskId, bool& isNewEntry);
                std::shared_ptr<hpc::data::JobInfo> RemoveJob(int jobId);
                void RemoveTask(int jobId, int taskId, long long attemptId);
                std::shared_ptr<hpc::data::TaskInfo> GetTask(int jobId, int taskId);

                void RequestResync()
                {
                    // Set this flag so the next report will trigger the resync with scheduler.
                    this->nodeInfo.JustStarted = true;
                }

            protected:
            private:
                pthread_rwlock_t lock;
                hpc::data::NodeInfo nodeInfo;
        };
    }
}

#endif // JOBTASKTABLE_H
