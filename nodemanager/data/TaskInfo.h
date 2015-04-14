#ifndef TASKINFO_H
#define TASKINFO_H

#include <cpprest/json.h>
#include <vector>
#include <string>
#include <memory>

namespace hpc
{
    namespace data
    {
        struct TaskInfo
        {
            public:
                TaskInfo(int jobId, int taskId, const std::string& nodeName) :
                    NodeName(nodeName), JobId(jobId), TaskId(taskId), TaskRequeueCount(0),
                    ExitCode(0), Exited(false), KernelProcessorTime(0), UserProcessorTime(0),
                    WorkingSet(0), NumberOfProcesses(0), IsPrimaryTask(true)
                {
                }

                TaskInfo(TaskInfo&& t) = default;

                web::json::value ToJson() const;
                web::json::value ToCompletionEventArgJson() const;

                const std::string& NodeName;

                long long GetAttemptId() const
                {
                    return ((long long)TaskRequeueCount << 32) + TaskId;
                }

                int JobId;
                int TaskId;
                int TaskRequeueCount;
                int ExitCode;
                bool Exited;
                long long KernelProcessorTime;
                long long UserProcessorTime;
                int WorkingSet;
                int NumberOfProcesses;
                bool IsPrimaryTask;

                std::string Message;
                std::vector<int> ProcessIds;
            protected:
            private:

        };
    }
}

#endif // TASKINFO_H
