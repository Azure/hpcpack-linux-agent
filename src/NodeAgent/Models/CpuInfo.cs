namespace NodeAgent.Models;

public class CpuInfo : DiagBase
{
    public int Cores { get; set; }

    public int Sockets { get; set; }
}
