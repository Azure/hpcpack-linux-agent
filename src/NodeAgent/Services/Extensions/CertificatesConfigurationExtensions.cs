using NodeAgent.Models;
using NodeAgent.Utils;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;

namespace NodeAgent.Services.Extensions;

public static class CertificatesConfigurationExtensions
{
    public static X509Certificate2Collection GetTrustedCAStore(this IConfiguration configuration)
    {
        var certs = configuration.GetSection("Certificates").Get<Certificates>();
        if (certs == null)
        {
            throw new InvalidOperationException("'Certificates' section is not found in configuration.");
        }
        Validator.ValidateObject(certs, new ValidationContext(certs));
        return CertificateReader.GetTrustedCAStore(certs);
    }
}
