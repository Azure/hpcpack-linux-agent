using System.ComponentModel.DataAnnotations;

namespace NodeAgent.Models;

public class Certificates : DiagBase, IValidatableObject
{
    [Required]
    public string CertificateChainFile { get; set; } = default!;

    public string? TrustedCAFile { get; set; }

    public string? TrustedCAPath { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!File.Exists(CertificateChainFile))
        {
            yield return new ValidationResult($"CertificateChainFile '{CertificateChainFile}' doesn't exist.", [nameof(CertificateChainFile)]);
        }

        if (!string.IsNullOrEmpty(TrustedCAFile) && !File.Exists(TrustedCAFile))
        {
            yield return new ValidationResult($"TrustedCAFile '{TrustedCAFile}' doesn't exist.", [nameof(TrustedCAFile)]);
        }

        if (!string.IsNullOrEmpty(TrustedCAPath) && !Directory.Exists(TrustedCAPath))
        {
            yield return new ValidationResult($"TrustedCAPath '{TrustedCAPath}' doesn't exist.", [nameof(TrustedCAPath)]);
        }
    }
}
