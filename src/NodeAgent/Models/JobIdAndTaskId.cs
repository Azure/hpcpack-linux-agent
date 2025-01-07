namespace NodeAgent.Models;

public class JobIdAndTaskId : DiagBase
{
    public int JobId { get; set; }

    public int TaskId { get; set; }

    //TODO/Q: what is this used for?
    public IEnumerable<int>? ResIds { get; set; }
}
