namespace NodeAgent.Models;

public class MetricCounter : DiagBase
{
    public string? Path { get; set; }

    public int MetricId { get; set; }

    public int InstanceId { get; set; }

    public string? InstanceName { get; set; }
}
