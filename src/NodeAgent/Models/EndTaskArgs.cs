namespace NodeAgent.Models;

public class EndTaskArgs
{
    public int JobId { get; set; }

    public int TaskId { get; set; }

    public int TaskCancelGracePeriodSeconds { get; set; }
}
