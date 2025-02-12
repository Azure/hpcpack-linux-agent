
using NodeAgent.Utils;

namespace NodeAgent.Models;

public interface IReadOnlyJobInfo
{
    int JobId { get; }

    IReadOnlyDictionary<int, IReadOnlyTaskInfo> Tasks { get; }
}

public class JobInfo : DiagBase, IReadOnlyJobInfo
{
    public int JobId { get; set; }

    public IDictionary<int, TaskInfo> Tasks { get; set; } = new Dictionary<int, TaskInfo>();

    IReadOnlyDictionary<int, IReadOnlyTaskInfo> IReadOnlyJobInfo.Tasks => new ProxyReadOnlyDictionary<int, IReadOnlyTaskInfo, TaskInfo>(Tasks);
}
