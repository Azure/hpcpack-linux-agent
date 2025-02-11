using NodeAgent.Models;

namespace NodeAgent.Utils;


//The public methods are not thread-safe. The caller has to ensure the thread-safety.
public class JobTaskTable
{
    private IDictionary<int, JobInfo> _jobs = new Dictionary<int, JobInfo>();

    //Add jobs task if it's not existed in _jobs. Return the existed or new TaskInfo.
    public TaskInfo AddJobAndTask(int jobId, int taskId, out bool isNewEntry)
    {
        if (!_jobs.TryGetValue(jobId, out var jobInfo))
        {
            jobInfo = new JobInfo() { JobId = jobId };
            _jobs.Add(jobId, jobInfo);
        }

        if (jobInfo.Tasks.TryGetValue(taskId, out var taskInfo))
        {
            isNewEntry = false;
        }
        else
        {
            isNewEntry = true;
            taskInfo = new TaskInfo() { JobId = jobId, TaskId = taskId };
            jobInfo.Tasks.Add(taskId, taskInfo);
        }

        return taskInfo;
    }

    public TaskInfo? GetTask(int jobId, int taskId)
    {
        if (_jobs.TryGetValue(jobId, out var jobInfo)
            && jobInfo.Tasks.TryGetValue(taskId, out var taskInfo))
        {
            return taskInfo;
        }
        return null;
    }

    public TaskInfo? RemoveTask(int jobId, int taskId, ulong attemptId)
    {
        TaskInfo? taskInfo = null;
        if (_jobs.TryGetValue(jobId, out var jobInfo))
        {
            if (jobInfo.Tasks.TryGetValue(taskId, out taskInfo) && taskInfo.AttemptId == attemptId)
            {
                jobInfo.Tasks.Remove(taskId);
            }
        }
        return taskInfo;
    }

    public JobInfo? RemoveJob(int jobId)
    {
        if (_jobs.TryGetValue(jobId, out var jobInfo))
        {
            _jobs.Remove(jobId);
        }
        return jobInfo;
    }

    public int GetJobCount()
    {
        return _jobs.Count;
    }

    public int GetTaskCount()
    {
        return GetTasks().Count();
    }

    public int GetCoresInUse()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<TaskInfo> GetTasks()
    {
        foreach (var job in _jobs.Values)
        {
            if (job.Tasks == null)
            {
                continue;
            }
            foreach (var task in job.Tasks.Values)
            {
                yield return task;
            }
        }
    }

    public IEnumerable<JobInfo> GetJobs(bool copy = false)
    {
        foreach (var job in _jobs.Values)
        {
            yield return copy ? job.Copy() : job;
        }
    }
}
