using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

public class StartTaskArgs
{
    public int JobId { get; set; }

    public int TaskId { get; set; }

    [Required]
    public ProcessStartInfo StartInfo { get; set; } = default!;
}
