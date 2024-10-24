using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

//TODO: A JSON deserializer is needed, since the original JSON is from a tuple!
//Or use some other method to create an instance of this from JSON.
public class StartTaskArgs
{
    public int JobId { get; set; }

    public int TaskId { get; set; }

    [Required]
    public ProcessStartInfo StartInfo { get; set; } = default!;
}
