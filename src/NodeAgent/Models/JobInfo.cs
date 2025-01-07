

namespace NodeAgent.Models;

public class JobInfo : DiagBase
{
    public int JobId { get; set; }

    public IDictionary<int, TaskInfo> Tasks { get; set; } = new Dictionary<int, TaskInfo>();

    //Return a deep clone
    public JobInfo Copy()
    {
        throw new NotImplementedException();
    }
}
