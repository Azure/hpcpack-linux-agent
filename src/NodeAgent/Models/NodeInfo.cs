namespace NodeAgent.Models;

public class NodeInfo
{
    public string? Name { get; set; }

    public string? MacAddress { get; set; }

    public int Availability {  get; set; }

    public bool JustStarted { get; set; }

    public IEnumerable<JobInfo>? Jobs { get; set; }
}
