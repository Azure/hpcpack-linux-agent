using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

public class RegisterInfo : DiagBase
{
    [Required]
    public string NodeName { get; set; } = default!;

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
