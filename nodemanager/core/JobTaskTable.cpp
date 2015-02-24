#include "JobTaskTable.h"
#include "../utils/WriterLock.h"
#include "../utils/ReaderLock.h"

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

std::shared_ptr<TaskInfo> JobTaskTable::AddJobAndTask(int jobId, int taskId)
{
    WriterLock writerLock(&this->lock);

    std::shared_ptr<JobInfo> job;
    auto j = this->nodeInfo.Jobs.find(jobId);
    if (j == this->nodeInfo.Jobs.end())
    {
        job = std::shared_ptr<JobInfo>(new JobInfo(jobId));
        this->nodeInfo.Jobs[jobId] = job;
    }
    else
    {
        job = j->second;
    }

    std::shared_ptr<TaskInfo> task;
    auto t = job->Tasks.find(taskId);
    if (t == job->Tasks.end())
    {
        task = std::shared_ptr<TaskInfo>(new TaskInfo(jobId, taskId));
        job->Tasks[taskId] = task;
    }
    else
    {
        task = t->second;
    }

    return task;
}

std::shared_ptr<JobInfo> JobTaskTable::RemoveJob(int jobId)
{
    WriterLock writerLock(&this->lock);

    std::shared_ptr<JobInfo> job;
    auto j = this->nodeInfo.Jobs.find(jobId);
    if (j != this->nodeInfo.Jobs.end())
    {
        job = j->second;
        this->nodeInfo.Jobs.erase(jobId);
    }

    return job;
}

void JobTaskTable::RemoveTask(int jobId, int taskId)
{
    WriterLock writerLock(&this->lock);

    std::shared_ptr<JobInfo> job;
    auto j = this->nodeInfo.Jobs.find(jobId);
    if (j != this->nodeInfo.Jobs.end())
    {
        job = j->second;
        job->Tasks.erase(taskId);
    }
}
