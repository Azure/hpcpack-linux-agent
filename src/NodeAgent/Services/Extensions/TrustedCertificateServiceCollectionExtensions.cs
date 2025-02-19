using System.Security.Cryptography.X509Certificates;

namespace NodeAgent.Services.Extensions;

public static class TrustedCertificateServiceCollectionExtensions
{
    public static IServiceCollection AddTrustedCertificateCollection(this IServiceCollection services)
    {
        return services.AddSingleton(provider =>
        {
            var certCollection = new X509Certificate2Collection();
            var loggerFactory = provider.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger(nameof(TrustedCertificateServiceCollectionExtensions));

            try
            {
                var configManager = provider.GetRequiredService<IConfigManager>();
                var certPaths = new List<string>();
                if (!string.IsNullOrEmpty(configManager.Config.CertificateChainFile))
                {
                    logger?.LogDebug("CertificateChainFile: {path}", configManager.Config.CertificateChainFile);
                    certPaths.Add(configManager.Config.CertificateChainFile);
                }
                if (!string.IsNullOrEmpty(configManager.Config.TrustedCAFile))
                {
                    logger?.LogDebug("TrustedCAFile: {path}", configManager.Config.TrustedCAFile);
                    certPaths.Add(configManager.Config.TrustedCAFile);
                }
                if (!string.IsNullOrEmpty(configManager.Config.TrustedCAPath))
                {
                    logger?.LogDebug("TrustedCAPath: {path}", configManager.Config.TrustedCAPath);
                    var files = Directory.GetFiles(configManager.Config.TrustedCAPath, "*").Where(f => f.EndsWith(".crt") || f.EndsWith(".pem"));
                    foreach (var file in files)
                    {
                        certPaths.Add(file);
                    }
                }

                foreach (var path in certPaths)
                {
                    logger?.LogDebug("Reading certificate from {path}", path);
                    var content = File.ReadAllText(path);
                    certCollection.Add(X509Certificate2.CreateFromPem(content));
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error when getting trusted certificates.");
                throw;
            }

            return certCollection;
        });
    }
}
