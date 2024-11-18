namespace NodeAgent.Models;

public class GpuInfo
{
    public string? Name { get; set; }

    public string? Uuid { get; set; }

    public string? PciBusDevice { get; set; }

    public string? PciBusId { get; set; }

    public long TotalMemory { get; set; }

    public long MaxSMClock { get; set; }
}
