using System.Security.Cryptography.X509Certificates;

namespace NodeAgent.Services.Extensions;

public static class HttpClientFactoryServiceCollectionExtensions
{
    public static IServiceCollection ConfigureDefaultHttpClientFactory(this IServiceCollection services, X509Certificate2Collection trustedCAStore)
    {
        return services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.ConfigurePrimaryHttpMessageHandler((provider) =>
            {
                var loggerFactory = provider.GetService<ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger(nameof(HttpClientFactoryServiceCollectionExtensions));
                try
                {
                    var handler = new HttpClientHandler();
                    handler.ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) =>
                    {
                        if (errors == System.Net.Security.SslPolicyErrors.None)
                        {
                            return true;
                        }
                        try
                        {
                            chain!.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                            chain.ChainPolicy.CustomTrustStore.AddRange(trustedCAStore);
                            return chain.Build(certificate!);
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "Error in ServerCertificateCustomValidationCallback.");
                            throw;
                        }
                    };
                    return handler;
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error when configuring primary HttpMessageHandler.");
                    throw;
                }
            });

            clientBuilder.ConfigureHttpClient((provider, client) =>
            {
                var configManager = provider.GetRequiredService<IConfigManager>();
                client.DefaultRequestHeaders.Add("AuthenticationKey", configManager.Config.ClusterAuthenticationKey);
            });
        });
    }
}
