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
                    filterFiles[JobStartFilter] = "filters/OnJobTaskStart.sh";
                    filterFiles[JobEndFilter] = "filters/OnJobEnd.sh";
                    filterFiles[TaskStartFilter] = "filters/OnTaskEnd.sh";
                }

                pplx::task<json::value> OnJobStart(int jobId, int taskId, int requeueCount, const json::value& input);
                pplx::task<json::value> OnJobEnd(int jobId, const json::value& input);
                pplx::task<json::value> OnTaskStart(int jobId, int taskId, int requeueCount, const json::value& input);
                pplx::task<json::value> ExecuteFilter(const std::string& filterType, int jobId, int taskId, int requeueCount, const json::value& input);

            private:
                std::map<std::string, std::string> filterFiles;
                const std::string JobStartFilter = "JobStartFilter";
                const std::string JobEndFilter = "JobEndFilter";
                const std::string TaskStartFilter = "TaskStartFilter";
        };
    }
}

#endif
