#include "JobTaskTable.h"
#include "../utils/WriterLock.h"
#include "../utils/ReaderLock.h"
#include "../utils/System.h"
#include <math.h>

using namespace hpc::core;
using namespace web;
using namespace hpc::data;
using namespace hpc::utils;

JobTaskTable* JobTaskTable::instance = nullptr;

json::value JobTaskTable::ToJson()
{
    ReaderLock readerLock(&this->lock);
    auto j = this->nodeInfo.ToJson();
    return std::move(j);
}

int JobTaskTable::GetTaskCount()
{
    ReaderLock readerLock(&this->lock);
    int taskCount = 0;

    for_each(this->nodeInfo.Jobs.begin(), this->nodeInfo.Jobs.end(),
        [&taskCount] (auto& i) { taskCount += i.second->Tasks.size(); });

    return taskCount;
}

int JobTaskTable::GetCoresInUse()
{
    ReaderLock readerLock(&this->lock);

    bool allUsed = false;
    int cores, sockets;
    System::CPU(cores, sockets);

    std::vector<uint64_t> coresMask(ceil((float)cores / 64), 0);
    for_each(this->nodeInfo.Jobs.begin(), this->nodeInfo.Jobs.end(), [&coresMask, &allUsed] (auto& i)
    {
        for_each(i.second->Tasks.begin(), i.second->Tasks.end(), [&coresMask, &allUsed] (auto& t)
        {
            if (t.second->Affinity.empty())
            {
                allUsed = true;
            }
            else
            {
                for (size_t i = 0; i < coresMask.size(); i++)
                {
                    coresMask[i] |= t.second->Affinity[i];
                }
            }
        });
    });

    if (allUsed)
    {
        return cores;
    }

    int used = 0;
    uint64_t bit = 1;
    for (int i = 0; i < cores; i++)
    {
        if (coresMask[i / 64] & (bit << i % 64))
        {
            used++;
        }
    }

    return used;
}

std::shared_ptr<TaskInfo> JobTaskTable::AddJobAndTask(int jobId, int taskId, bool& isNewEntry)
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
        isNewEntry = true;
    }
    else
    {
        task = t->second;
        isNewEntry = false;
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
        this->nodeInfo.Jobs.erase(j);
    }

    return job;
}

void JobTaskTable::RemoveTask(int jobId, int taskId, uint64_t attemptId)
{
    WriterLock writerLock(&this->lock);

    auto j = this->nodeInfo.Jobs.find(jobId);
    if (j != this->nodeInfo.Jobs.end())
    {
        auto t = j->second->Tasks.find(taskId);

        // only erase when attempt ID matches.
        if (t != j->second->Tasks.end() &&
            t->second->GetAttemptId() == attemptId)
        {
            j->second->Tasks.erase(t);
        }
    }
}
