namespace NodeAgent.Models;

public class RegisterInfo
{
    public string? NodeName { get; set; }

    public int CoreCount { get; set; }

    public int SocketCount { get; set; }

    public ulong MemoryMegabytes { get; set; }

    public string? DistroInfo { get; set; }

    public string? AzureInstanceMetaData { get; set; }

    public string? CustomProperties { get; set; }

    public string? CcpVersion { get; set; }

    public NetworkInfo[]? NetworksInfo { get; set; }

    public GpuInfo[]? GpuInfo { get; set; }

    //TODO: Make it for logging purpose
    public override string? ToString()
    {
        return base.ToString();
    }
}
