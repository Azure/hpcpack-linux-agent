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
                TaskInfo(int jobId, int taskId, int taskRequeueCount) :
                    JobId(jobId), TaskId(taskId), TaskRequeueCount(taskRequeueCount),
                    ExitCode(0), Exited(false), KernelProcessorTime(0), UserProcessorTime(0),
                    WorkingSet(0), NumberOfProcesses(0), IsPrimaryTask(true)
                {
                }

                TaskInfo(TaskInfo&& t) = default;

                web::json::value GetJson() const;

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
