
namespace NodeAgent.Models;

public class NetworkInfo
{
    public string? Name { get; set; }

    public string? MacAddress { get; set; }

    public string? IpV4 { get; set; }

    public string? IpV6 { get; set; }

    public bool IsIB { get; set; }
}
