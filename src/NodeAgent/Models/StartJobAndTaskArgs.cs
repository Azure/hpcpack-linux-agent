using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

public class StartJobAndTaskArgs : DiagBase
{
    public int JobId { get; set; }

    public int TaskId { get; set; }

    [Required]
    public ProcessStartInfo StartInfo { get; set; } = default!;

    [Required]
    public string UserName { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;

    public string? PrivateKey { get; set; }

    public string? PublicKey { get; set; }

    public StartTaskArgs ToStartTaskArgs()
    {
        return new StartTaskArgs { JobId = JobId, TaskId = TaskId, StartInfo = StartInfo, };
    }
}
