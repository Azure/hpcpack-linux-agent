using NodeAgent.Models;
using NodeAgent.Services;

namespace NodeAgent.Test.Mocks;

public class MockSchedulerApiClientForJobTaskExecutor : ISchedulerApiClient
{
    public class TaskCompletionCall
    {
        public string? Uri { get; set; }

        public TaskCompletionEventArgs? Args { get; set; }
    }

    public IList<TaskCompletionCall> TaskCompletionCalls { get; private set; } = new List<TaskCompletionCall>();

    public Task<HostsUpdate?> GetHostsAsync(string? updateId, CancellationToken cancelToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<int> RegisterAsync(RegisterInfo info, CancellationToken cancelToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<int> ReportHeartbeatAsync(NodeInfo nodeInfo, CancellationToken cancelToken = default)
    {
        throw new NotSupportedException();
    }

    public Task ReportTaskCompletionAsync(string uri, TaskCompletionEventArgs args, CancellationToken cancelToken = default)
    {
        TaskCompletionCalls.Add(new TaskCompletionCall() { Uri = uri, Args = args });
        return Task.CompletedTask;
    }
}
