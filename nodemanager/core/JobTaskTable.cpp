#include "JobTaskTable.h"
#include "../utils/WriterLock.h"
#include "../utils/ReaderLock.h"

using namespace hpc::core;
using namespace web;
using namespace hpc::data;

json::value JobTaskTable::ToJson()
{
    ReaderLock readerLock(&this->lock);
    auto j = this->nodeInfo.ToJson();
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
        task = std::shared_ptr<TaskInfo>(new TaskInfo(jobId, taskId, nodeInfo.Name));
        job->Tasks[taskId] = task;
    }
    else
    {
        task = t->second;
    }

    return task;
}

std::shared_ptr<TaskInfo> JobTaskTable::GetTask(int jobId, int taskId)
{
    ReaderLock readerLock(&this->lock);

    std::shared_ptr<TaskInfo> task;

    auto j = this->nodeInfo.Jobs.find(jobId);
    if (j != this->nodeInfo.Jobs.end())
    {
        auto t = j->second->Tasks.find(taskId);
        if (t != j->second->Tasks.end())
        {
            task = t->second;
        }
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

    auto j = this->nodeInfo.Jobs.find(jobId);
    if (j != this->nodeInfo.Jobs.end())
    {
        j->second->Tasks.erase(taskId);
    }
}
