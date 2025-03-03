using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NodeAgent.Models;

public class HostEntry : DiagBase
{
    [Required]
    [JsonPropertyName("Name")]
    public string HostName { get; set; } = default!;

    [Required]
    [JsonPropertyName("Address")]
    public string IPAddress { get; set; } = default!;
}
