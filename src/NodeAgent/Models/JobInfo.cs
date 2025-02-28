using System.Text.Json.Serialization;

namespace NodeAgent.Models;

public interface IReadOnlyJobInfo
{
    int JobId { get; }

    IEnumerable<IReadOnlyTaskInfo> Tasks { get; }
}

public class JobInfo : DiagBase, IReadOnlyJobInfo
{
    public int JobId { get; set; }

    /*
     * NOTE:
     *
     * The Tasks property of type IDictionary must be ignored in JSON output, as the scheduler expects
     * a Tasks property of array type in JSON, though IReadOnlyJobInfo is returned in IJobTaskExecutor.EndJobAsync.
     * It seems JsonSerializer.Serialize doesn't use the IReadOnlyJobInfo but the implementing type JobInfo.
     */
    [JsonIgnore]
    public IDictionary<int, TaskInfo> Tasks { get; set; } = new Dictionary<int, TaskInfo>();

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
