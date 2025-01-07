namespace NodeAgent.Models;

//NOTE: The unit is KB for all properties.
//TODO: Is uint/int is enough instead of ulong?
public class MemoryUsage : DiagBase
{
    public ulong Total {  get; set; }

    public ulong Available { get; set; }
}
