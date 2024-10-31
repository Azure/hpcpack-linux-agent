namespace NodeAgent.Models;

public enum NodeAvailability
{
    AlwaysOn = 0,
    Available = 1,
    Occupied = 2
};

public class NodeInfo
{
    public string? Name { get; set; }

    //TODO: It's never set?
    public string? MacAddress { get; set; }

    //TODO: It's never changed?
    public int Availability { get; set; } = (int)NodeAvailability.AlwaysOn;

    public bool JustStarted { get; set; }

    public IEnumerable<JobInfo>? Jobs { get; set; }
}
