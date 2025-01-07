namespace NodeAgent.Models;

public class RegisterInfo : DiagBase
{
    public string? NodeName { get; set; }

    public int CoreCount { get; set; }

    public int SocketCount { get; set; }

    public ulong MemoryMegabytes { get; set; }

    public string? DistroInfo { get; set; }

    public string? AzureInstanceMetaData { get; set; }

    public string? CustomProperties { get; set; }

    public string? CcpVersion { get; set; }

    public IEnumerable<NetworkInfo>? NetworksInfo { get; set; }

    public IEnumerable<GpuInfo>? GpuInfo { get; set; }
}
