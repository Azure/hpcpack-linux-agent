using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

public class TaskCompletionEventArgs : DiagBase
{
    public int JobId { get; set; }

    [Required]
    public TaskInfo TaskInfo { get; set; } = default!;

    [Required]
    public string NodeName { get; set; } = default!;
}
