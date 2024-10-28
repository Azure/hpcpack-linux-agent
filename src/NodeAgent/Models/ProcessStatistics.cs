using System.Text.Json.Serialization;

namespace NodeAgent.Models;

public class ProcessStatistics
{
    public ulong UserTimeMs { get; set; }
    
    public ulong KernelTimeMs { get; set; }

    public ulong WorkingSetKb { get; set; }

    public IList<int> ProcessIds = new List<int>();

    [JsonIgnore]
    public int ProcessCount => ProcessIds.Count;

    [JsonIgnore]
    public bool IsTerminated => ProcessCount == 0;
}
