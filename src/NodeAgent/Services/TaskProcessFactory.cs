using NodeAgent.Models;

namespace NodeAgent.Services;

public interface ITaskProcessFactory
{
    ITaskProcess CreateProcess(
            int jobId,
            int taskId,
            int requeueCount,
            string taskExecutionName,
            string cmdLine,
            string? standardOut,
            string? standardErr,
            string? standardIn,
            string? workDir,
            string user,
            bool dumpStdoutToExecutionMessage,
            IEnumerable<ulong>? cpuAffinity,
            IDictionary<string, string>? env,
            Action<int, string, ProcessStatistics> onComplete);

    void Cleanup();
}

public class TaskProcessFactory : ITaskProcessFactory
{
    public ITaskProcess CreateProcess(
        int jobId,
        int taskId,
        int requeueCount,
        string taskExecutionName,
        string cmdLine,
        string? standardOut,
        string? standardErr,
        string? standardIn,
        string? workDir,
        string user,
        bool dumpStdoutToExecutionMessage,
        IEnumerable<ulong>? cpuAffinity,
        IDictionary<string, string>? env,
        Action<int, string, ProcessStatistics> onComplete)
    {
        throw new NotImplementedException();
    }

    public void Cleanup()
    {
        throw new NotImplementedException();
    }
}
