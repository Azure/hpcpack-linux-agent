namespace NodeAgent.Models;

public class MetricCountersConfig : DiagBase
{
    public IEnumerable<MetricCounter>? MetricCounters { get; set; }
}
