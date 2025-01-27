namespace NodeAgent.Models;

public class ProcessStartInfo : DiagBase
{
    public string? CommandLine { get; set; }

    public string? StdInFile { get; set; }

    public string? StdOutFile { get; set; }

    public string? StdErrFile { get; set; }

    public string? WorkDirectory { get; set; }

    public int TaskRequeueCount { get; set; }

    public IEnumerable<ulong>? Affinity {  get; set; }

    //TODO: When the value of a key can be null? Or IDictionary<string, string> is better.
    public IDictionary<string, string?>? EnvironmentVariables { get; set; }
}
