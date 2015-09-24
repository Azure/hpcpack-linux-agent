#ifndef TASKINFO_H
#define TASKINFO_H

#include <cpprest/json.h>
#include <vector>
#include <string>
#include <memory>

#include "../utils/Logger.h"
#include "../data/ProcessStatistics.h"

using namespace hpc::utils;

namespace hpc
{
    namespace data
    {
        struct TaskInfo
        {
            public:
                TaskInfo(int jobId, int taskId, const std::string& nodeName) :
                    NodeName(nodeName), JobId(jobId), TaskId(taskId)
                {
                }

                ~TaskInfo()
                {
                    this->CancelGracefulThread();
                }

                void CancelGracefulThread()
                {
                    if (this->GracefulThreadId)
                    {
                        pthread_cancel(this->GracefulThreadId);
                        pthread_join(this->GracefulThreadId, nullptr);
                        Logger::Debug("Destructed TaskInfo GracefulThread {0}", this->GracefulThreadId);
                        this->GracefulThreadId = 0;
                    }
                }

                TaskInfo(TaskInfo&& t) = default;

                web::json::value ToJson() const;
                web::json::value ToCompletionEventArgJson() const;

                const std::string& NodeName;

                uint64_t GetAttemptId() const
                {
                    return ((uint64_t)taskRequeueCount << 32) + TaskId;
                }

                int GetTaskRequeueCount() const { return this->taskRequeueCount; }

                void SetTaskRequeueCount(int c)
                {
                    int oldC = this->taskRequeueCount;
                    this->taskRequeueCount = c;

                    if (!this->processKeySet)
                    {
                        this->ProcessKey = this->GetAttemptId();
                        this->processKeySet = true;
                    }

                    hpc::utils::Logger::Info(this->JobId, this->TaskId, this->taskRequeueCount,
                        "Change requeue count from {0} to {1}, processKey {2}",
                        oldC, c, this->ProcessKey);
                }

                int GetProcessCount() const { return this->ProcessIds.size(); }

                void AssignFromStat(const ProcessStatistics& stat);

                int JobId;
                int TaskId;
                int ExitCode = 0;
                bool Exited = false;
                uint64_t KernelProcessorTimeMs = 0;
                uint64_t UserProcessorTimeMs = 0;
                uint64_t WorkingSetKb = 0;
                bool IsPrimaryTask = true;
                uint64_t ProcessKey;

                std::string Message;
                std::vector<int> ProcessIds;
                std::vector<uint64_t> Affinity;

                pthread_t GracefulThreadId = 0;
            protected:
            private:
                int taskRequeueCount = 0;
                bool processKeySet = false;

        };
    }
}

#endif // TASKINFO_H
