namespace NodeAgent.Models;

public class StartTaskArgs
{
    public int JobId { get; set; }

    public int TaskId { get; set; }

    public ProcessStartInfo StartInfo { get; set; }
}
