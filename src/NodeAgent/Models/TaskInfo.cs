
using System.Text.Json.Serialization;

namespace NodeAgent.Models;

public interface IReadOnlyTaskInfo
{
    int JobId { get; }

    int TaskId { get; }

    int TaskRequeueCount { get; }

    int ExitCode { get; }

    bool Exited { get; }

    //In MS
    ulong KernelProcessorTime { get; }

    //In MS
    ulong UserProcessorTime { get; }

    //In KB
    ulong WorkingSet { get; }

    int NumberOfProcesses { get; }

    bool PrimaryTask { get; }

    string? Message { get; }

    [JsonIgnore]
    IReadOnlyList<int>? ProcessIds { get; }

    /*
     * NOTE
     *
     * The scheduler expects a comma-spearated string rather than a list, of ProcessIds in JSON.
     */
    [JsonPropertyName("ProcessIds")]
    string ProcessIdsInString { get; }
}

public class TaskInfo : DiagBase, IReadOnlyTaskInfo
{
    public TaskInfo(int jobId, int taskId, int requeueCount)
    {
        JobId = jobId;
        TaskId = taskId;
        TaskRequeueCount = requeueCount;
        ProcessKey = ((ulong)TaskRequeueCount << 32) + (ulong)TaskId;
    }

    public int JobId { get; }

    public int TaskId { get; }

    public int TaskRequeueCount { get; }

    public int ExitCode { get; set; } = 0;

    public bool Exited { get; set; } = false;

    //In MS
    public ulong KernelProcessorTime { get; set; } = 0;

    //In MS
    public ulong UserProcessorTime { get; set; } = 0;

    //In KB
    public ulong WorkingSet { get; set; } = 0;

    public int NumberOfProcesses => ProcessIds?.Count ?? 0;

    public bool PrimaryTask { get; set; } = true;

    public string? Message { get; set; }

    public IList<int>? ProcessIds { get; set; }

    IReadOnlyList<int>? IReadOnlyTaskInfo.ProcessIds => ProcessIds is List<int> ? (List<int>)ProcessIds : null;

    string IReadOnlyTaskInfo.ProcessIdsInString => ProcessIds is null ? string.Empty : string.Join(',', ProcessIds);

    [JsonIgnore]
    public IEnumerable<ulong>? Affinity { get; set; }

    [JsonIgnore]
    public ulong ProcessKey { get; }

    [JsonIgnore]
    public CancellationTokenSource? CancelGracefulPeriod { get; set; }

    public void AssignFromStat(ProcessStatistics? stat)
    {
        KernelProcessorTime = stat?.KernelTimeMs ?? 0;
        UserProcessorTime = stat?.UserTimeMs ?? 0;
        ProcessIds = stat?.ProcessIds;
        WorkingSet = stat?.WorkingSetKb ?? 0;
    }
}
