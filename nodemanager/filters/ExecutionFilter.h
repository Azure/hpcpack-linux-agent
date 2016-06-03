#ifndef EXECUTIONFILTER_H
#define EXECUTIONFILTER_H

#include <string>
#include <cpprest/json.h>
#include "../core/NodeManagerConfig.h"

namespace hpc
{
    namespace filters
    {
        using namespace hpc::core;

        class ExecutionFilter
        {
            public:
                ExecutionFilter()
                {
                    filterFiles[JobStartFilter] = NodeManagerConfig::GetJobStartFilter();
                    filterFiles[JobEndFilter] = NodeManagerConfig::GetJobEndFilter();
                    filterFiles[TaskStartFilter] = NodeManagerConfig::GetTaskStartFilter();
                }

                int OnJobStart(int jobId, const json::value& input, json::value& output, std::string& executionMessage);
                int OnJobEnd(int jobId, const json::value& input, json::value& output, std::string& executionMessage);
                int OnTaskStart(int jobId, int taskId, int requeueCount, const json::value& input, json::value& output, std::string& executionMessage);
                int ExecuteFilter(const std::string& filterType, int jobId, int taskId, int requeueCount, const json::value& input, json::value& output, std::string& executionMessage);

            private:
                std::map<std::string, std::string> filterFiles;
                const std::string JobStartFilter = "JobStartFilter";
                const std::string JobEndFilter = "JobEndFilter";
                const std::string TaskStartFilter = "TaskStartFilter";
        };
    }
}

#endif
