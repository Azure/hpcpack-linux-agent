using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

public class ProcessStartInfo
{
    public string? CommandLine { get; set; }

    public string? StdInFile { get; set; }

    public string? StdOutFile { get; set; }

    public string? StdErrFile { get; set; }

    public string? WorkDirectory { get; set; }

    public int TaskRequeueCount { get; set; }

    public IList<ulong>? Affinity {  get; set; }

    [Required]
    public IDictionary<string, string> EnvironmentVariables { get; set; } = default!;
}
