using NodeAgent.Models;
using System.Security.Cryptography.X509Certificates;

namespace NodeAgent.Utils;

public static class CertificateReader
{
    public static X509Certificate2Collection GetTrustedCAStore(Certificates certificates)
    {
        var certCollection = new X509Certificate2Collection();
        var certPaths = new List<string>
        {
            certificates.CertificateChainFile
        };

        if (!string.IsNullOrEmpty(certificates.TrustedCAFile))
        {
            certPaths.Add(certificates.TrustedCAFile);
        }

        if (!string.IsNullOrEmpty(certificates.TrustedCAPath))
        {
            var files = Directory.GetFiles(certificates.TrustedCAPath, "*").Where(f => f.EndsWith(".crt") || f.EndsWith(".pem"));
            foreach (var file in files)
            {
                certPaths.Add(file);
            }
        }

        foreach (var path in certPaths)
        {
            var content = File.ReadAllText(path);
            certCollection.Add(X509Certificate2.CreateFromPem(content));
        }

        return certCollection;
    }
}
