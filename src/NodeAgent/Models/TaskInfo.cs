
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

    IReadOnlyList<int>? ProcessIds { get; }
}


//TODO: Unit test? And should it be a model class with unit test?
public class TaskInfo : DiagBase, IReadOnlyTaskInfo
{
    public int JobId { get; set; }

    public int TaskId { get; set; }

    /*
     * NOTE
     *
     * AttemptId is not always equal to ProcessKey, which is set only once.
     */
    public int TaskRequeueCount
    {
        set
        {
            if (value >= _taskRequeueCount)
            {
                _taskRequeueCount = value;
                if (!_processKeySet)
                {
                    ProcessKey = AttemptId;
                    _processKeySet = true;
                }
            }
        }

        get
        {
            return _taskRequeueCount;
        }
    }

    private int _taskRequeueCount = 0;

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

    [JsonIgnore]
    public IEnumerable<ulong>? Affinity { get; set; }

    [JsonIgnore]
    public ulong ProcessKey { get; private set; }

    private bool _processKeySet = false;

    [JsonIgnore]
    public ulong AttemptId => ((ulong)_taskRequeueCount << 32) + (ulong)TaskId;

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
