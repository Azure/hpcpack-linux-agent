using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

//TODO: A JSON deserializer is needed, since the original JSON is from a tuple!
//Or use some other method to create an instance of this from JSON.
public class StartJobAndTaskArgs
{
    public int JobId { get; set; }

    public int TaskId { get; set; }

    [Required]
    public ProcessStartInfo StartInfo { get; set; } = default!;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    //TODO: For SoftCard credential, base64 encoded
    //string Certificate;

    public string? PrivateKey { get; set; }

    public string? PublicKey { get; set; }
}
