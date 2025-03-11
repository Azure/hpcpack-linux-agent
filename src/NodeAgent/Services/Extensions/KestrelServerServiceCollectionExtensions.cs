using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Security.Cryptography.X509Certificates;

namespace NodeAgent.Services.Extensions;

public static class KestrelServerServiceCollectionExtensions
{
    public static IServiceCollection ConfigureKestrelServer(this IServiceCollection services, X509Certificate2Collection trustedCAStore)
    {
        services.Configure<KestrelServerOptions>(options =>
        {
            options.ConfigureHttpsDefaults(options =>
            {
                options.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                options.ClientCertificateValidation = (certificate, chain, errors) =>
                {
                    if (errors == System.Net.Security.SslPolicyErrors.None)
                    {
                        return true;
                    }
                    chain!.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain.ChainPolicy.CustomTrustStore.AddRange(trustedCAStore);
                    return chain.Build(certificate);
                };
            });
        });

        return services;
    }
}
