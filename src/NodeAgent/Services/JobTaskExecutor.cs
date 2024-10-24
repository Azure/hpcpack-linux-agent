using NodeAgent.Models;

namespace NodeAgent.Services;

public interface IJobTaskExecutor
{
    void StartJobAndTask(StartJobAndTaskArgs args, string callbackUri);

    void StartTask(StartTaskArgs args, string callbackUri);

    TaskInfo EndTask(EndTaskArgs args, string callbackUri);

    JobInfo EndJob(EndJobArgs args);

    string PeekTaskOutput(PeekTaskOutputArgs args);
}

public class JobTaskExecutor : IJobTaskExecutor
{
    public JobInfo EndJob(EndJobArgs args)
    {
        throw new NotImplementedException();
    }

    public TaskInfo EndTask(EndTaskArgs args, string callbackUri)
    {
        throw new NotImplementedException();
    }

    public string PeekTaskOutput(PeekTaskOutputArgs args)
    {
        throw new NotImplementedException();
    }

    public void StartJobAndTask(StartJobAndTaskArgs args, string callbackUri)
    {
        throw new NotImplementedException();
    }

    public void StartTask(StartTaskArgs args, string callbackUri)
    {
        throw new NotImplementedException();
    }
}
