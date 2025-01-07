using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

public class OutputData : DiagBase
{
    [Required]
    public string? NodeName { get; set; }

    [Required]
    public int Order { get; set; } = 0;

    [Required]
    public string? Content { get; set; }

    [Required]
    public bool Eof { get; set; } = false;
}
