using NodeAgent.Models;

namespace NodeAgent.Services;

public interface ITaskProcess
{
    Task<Tuple<int, int>> StartAsync(CancellationToken cancellationToken = default);

    Task KillAsync(int forcedExitCode = 0x0FFFFFFF, bool forced = true, CancellationToken cancellationToken = default);

    Task<ProcessStatistics> GetStatisticsFromCGroupAsync(CancellationToken cancellationToken = default);

    Task<string> PeekOutputAsync(CancellationToken cancellationToken = default);
}

public class TaskProcess : ITaskProcess
{
    public TaskProcess(
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

    public Task<Tuple<int, int>> StartAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task KillAsync(int forcedExitCode = 268435455, bool forced = true, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<ProcessStatistics> GetStatisticsFromCGroupAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<string> PeekOutputAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
