#ifndef JOBTASKTABLE_H
#define JOBTASKTABLE_H

#include <map>
#include <cpprest/json.h>

#include "../data/TaskInfo.h"
#include "../data/JobInfo.h"
#include "../data/NodeInfo.h"
#include "../utils/ReaderLock.h"

namespace hpc
{
    namespace core
    {
        class JobTaskTable
        {
            public:
                JobTaskTable() : lock(PTHREAD_RWLOCK_INITIALIZER)
                {
                    JobTaskTable::instance = this;
                }

                ~JobTaskTable()
                {
                    pthread_rwlock_destroy(&this->lock);
                    JobTaskTable::instance = nullptr;
                }

                web::json::value ToJson();
             //   web::json::value GetTaskJson(int jobId, int taskId) const;

                std::shared_ptr<hpc::data::TaskInfo> AddJobAndTask(int jobId, int taskId, bool& isNewEntry);
                std::shared_ptr<hpc::data::JobInfo> RemoveJob(int jobId);
                void RemoveTask(int jobId, int taskId, uint64_t attemptId);
                std::shared_ptr<hpc::data::TaskInfo> GetTask(int jobId, int taskId);
                int GetJobCount()
                {
                    ReaderLock readerLock(&this->lock);
                    return this->nodeInfo.Jobs.size();
                }

                int GetTaskCount();

                int GetCoresInUse();

                void RequestResync()
                {
                    // Set this flag so the next report will trigger the resync with scheduler.
                    this->nodeInfo.JustStarted = true;
                }

                static JobTaskTable* GetInstance() { return JobTaskTable::instance; }

            protected:
            private:
                pthread_rwlock_t lock;
                hpc::data::NodeInfo nodeInfo;

                static JobTaskTable* instance;
        };
    }
}

#endif // JOBTASKTABLE_H
