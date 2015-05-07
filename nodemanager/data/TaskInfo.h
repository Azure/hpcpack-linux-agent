#ifndef TASKINFO_H
#define TASKINFO_H

#include <cpprest/json.h>
#include <vector>
#include <string>
#include <memory>

#include "../utils/Logger.h"
#include "../data/ProcessStatistics.h"

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

                TaskInfo(TaskInfo&& t) = default;

                web::json::value ToJson() const;
                web::json::value ToCompletionEventArgJson() const;

                const std::string& NodeName;

                long long GetAttemptId() const
                {
                    return ((long long)taskRequeueCount << 32) + TaskId;
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
                long long ProcessKey;

                std::string Message;
                std::vector<int> ProcessIds;
            protected:
            private:
                int taskRequeueCount = 0;
                bool processKeySet = false;

        };
    }
}

#endif // TASKINFO_H
