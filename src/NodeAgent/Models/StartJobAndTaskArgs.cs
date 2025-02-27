using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

//TODO: Make it a subclass of StartTaskArgs
public class StartJobAndTaskArgs : DiagBase, IValidatableObject
{
    public int JobId { get; set; }

    public int TaskId { get; set; }

    [Required]
    public ProcessStartInfo StartInfo { get; set; } = default!;

    [Required]
    public string UserName { get; set; } = default!;

    public string? Password { get; set; }

    public string? PrivateKey { get; set; }

    public string? PublicKey { get; set; }

    public StartTaskArgs ToStartTaskArgs()
    {
        return new StartTaskArgs { JobId = JobId, TaskId = TaskId, StartInfo = StartInfo, };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (string.IsNullOrEmpty(Password) && string.IsNullOrEmpty(PrivateKey))
        {
            yield return new ValidationResult(
                "Either a password or a priveate key must be provided.",
                [nameof(Password), nameof(PrivateKey)]);
        }
    }
}
