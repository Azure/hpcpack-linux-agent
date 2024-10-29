using NodeAgent.Models;

namespace NodeAgent.Utils;

public class Process
{
    public Process(
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

    public Task<Tuple<int, int>> StartAsync()
    {
        throw new NotImplementedException();
    }

    public void Kill(int forcedExitCode = 0x0FFFFFFF, bool forced = true)
    {
        throw new NotImplementedException();
    }

    public ProcessStatistics GetStatisticsFromCGroup()
    {
        throw new NotImplementedException();
    }

    public string PeekOutput()
    {
        throw new NotImplementedException();
    }

    public static void Cleanup()
    {
        throw new NotImplementedException();
    }

}
