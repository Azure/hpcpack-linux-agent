using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

public class OutputData : DiagBase
{
    [Required]
    public string NodeName { get; set; } = default!;

    public int Order { get; set; } = 0;

    [Required]
    public string Content { get; set; } = default!;

    public bool Eof { get; set; } = false;
}
