using System.Text.Json.Serialization;

namespace NodeAgent.Models;

public interface IReadOnlyJobInfo
{
    int JobId { get; }

    /*
     * NOTE
     *
     * The scheduler expects a list rather than a hash, of Tasks in JSON.
     */
    IEnumerable<IReadOnlyTaskInfo> Tasks { get; }
}

public class JobInfo : DiagBase, IReadOnlyJobInfo
{
    public int JobId { get; set; }

    [JsonIgnore]
    public IDictionary<int, TaskInfo> Tasks { get; set; } = new Dictionary<int, TaskInfo>();

    /*
     * NOTE
     *
     * There's a problem in APS.NET controller action (EndJob) that even IReadOnlyJobInfo is the return type,
     * JobInfo is serialized in JSON. So here we have to force the JSON serializer to include the (non-public)
     * IReadOnlyJobInfo.Tasks as "Tasks" and ignore the public IDictionary Tasks.
     */
    [JsonInclude]
    [JsonPropertyName("Tasks")]
    IEnumerable<IReadOnlyTaskInfo> IReadOnlyJobInfo.Tasks
    {
        get
        {
            foreach (var (k, v) in Tasks)
            {
                yield return v;
            }
        }
    }
}
