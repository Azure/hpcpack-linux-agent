namespace NodeAgent.Models;

public class CommandResult
{
    public int ExitCode { get; set; }

    public string? StdOut { get; set; }

    public string? StdErr { get; set; }

    public override string ToString()
    {
        return $"Exit code: {ExitCode}\nStdOut:\n{StdOut}\nStdErr:\n{StdErr}";
    }
}
