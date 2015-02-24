#include "JobTaskTable.h"

using namespace hpc::core;
using namespace web;
using namespace hpc::data;

json::value JobTaskTable::GetNodeJson() const
{
    return this->nodeInfo.ToJson();
}

json::value JobTaskTable::GetMetricJson() const
{
    auto j = json::value();
    return std::move(j);
}

json::value JobTaskTable::GetTaskJson(int jobId, int taskId) const
{
    auto j = json::value();
    return std::move(j);
}

void JobTaskTable::AddJobAndTask(int jobId, int taskId, TaskInfo&& taskInfo)
{
}

JobInfo JobTaskTable::RemoveJob(int jobId)
{
    JobInfo j(0);
    return std::move(j);
}

TaskInfo JobTaskTable::RemoveTask(int jobId, int taskId)
{
    TaskInfo t(0, 0, 0);
    return std::move(t);
}
