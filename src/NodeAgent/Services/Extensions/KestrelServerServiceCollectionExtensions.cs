using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Security.Cryptography.X509Certificates;

namespace NodeAgent.Services.Extensions;

public static class KestrelServerServiceCollectionExtensions
{
    public static IServiceCollection ConfigureKestrelServerOptions(this IServiceCollection services)
    {
        return services.Configure<KestrelServerOptions>(options =>
        {
            options.ConfigureHttpsDefaults(options =>
            {
                //NOTE: The Configure method doesn't provide a service provider so we have to build one in this scope.
                //This is not ideal but better than nothing.
                using var provider = services.BuildServiceProvider();
                var loggerFactory = provider.GetService<ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger(nameof(KestrelServerServiceCollectionExtensions));
                try
                {
                    var configManager = provider.GetRequiredService<IConfigManager>();
                    var trustedCerts = provider.GetRequiredService<X509Certificate2Collection>();
                    //Create a new cert collection from the one from the service provider, which will be disposed out of this scope.
                    var trustedCertsCopy = new X509Certificate2Collection(trustedCerts);

                    options.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                    options.ServerCertificate = X509Certificate2.CreateFromPemFile(
                        configManager.Config.CertificateChainFile, configManager.Config.PrivateKeyFile);
                    options.ClientCertificateValidation = (certificate, chain, errors) =>
                    {
                        //NOTE: A logger is needed here but we cannot use the one in the parent scope since the latter will be disposed
                        //when the provider we build is disposed in the parent scope.
                        if (errors == System.Net.Security.SslPolicyErrors.None)
                        {
                            return true;
                        }
                        chain!.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                        chain.ChainPolicy.CustomTrustStore.AddRange(trustedCertsCopy);
                        return chain.Build(certificate);
                    };
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error when configuring KestrelServerOptions");
                    throw;
                }
            });
        });
    }
}
