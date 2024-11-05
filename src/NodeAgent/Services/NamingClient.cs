using System.Collections.Concurrent;

namespace NodeAgent.Services;

//All methods of the interface are thread-safe.
public interface INamingClient
{
    Task<string> GetServiceLocationAsync(string serviceName, CancellationToken cancellationToken = default);

    void InvalidateCache();
}

public class NamingClient : INamingClient
{
    private ILogger _logger;
    private IConfigManager _configManager;
    private IHttpClientFactory _httpClientFactory;

    private IDictionary<string, string> _serviceLocations = new ConcurrentDictionary<string, string>();
    private object _lock = new object();

    public NamingClient(ILogger<NamingClient> logger, IConfigManager configManager, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configManager = configManager;
        _httpClientFactory = httpClientFactory;
    }

    private async Task<string> RequestForServiceLocation(string serviceName, CancellationToken cancellationToken)
    {
        var namingServicesUris = _configManager.Config.NamingServiceUri;
        var idx = Random.Shared.Next(namingServicesUris.Length);
        var intervalSeconds = 1;

        while (!cancellationToken.IsCancellationRequested)
        {
            var uri = $"{namingServicesUris[idx++]}{serviceName}";
            idx %= namingServicesUris.Length;

            _logger.LogInformation("Request service location for '{name}' from '{uri}'.", serviceName, uri);

            var httpClient = _httpClientFactory.CreateClient();
            try
            {
                var response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var location = await response.Content.ReadFromJsonAsync<string>(cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Got location '{location}' for service '{name}'", location, serviceName);

                if (string.IsNullOrEmpty(location))
                {
                    throw new InvalidDataException($"Got an empty location from {uri}");
                }
                return location;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when requesting '{uri}'", uri);
                if (ex is InvalidDataException)
                {
                    throw;
                }
                /*
                 * NOTE/TODO
                 *
                 * what if serviceName is invalid, or the server never returns OK? It will be a infinite loop then,
                 * unless being cancelled. This behavior inherites the one from the C++ version.
                 */
            }

            await Task.Delay(intervalSeconds * 1000, cancellationToken).ConfigureAwait(false);

            intervalSeconds *= 2;
            if (intervalSeconds > 60)
            {
                intervalSeconds = 60;
            }
        }

        throw new OperationCanceledException(cancellationToken);
    }

    public async Task<string> GetServiceLocationAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (_serviceLocations.TryGetValue(serviceName, out var serviceLocation))
        {
            return serviceLocation;
        }

        await Task.Yield();

        lock (_lock)
        {
            if (_serviceLocations.TryGetValue(serviceName, out serviceLocation))
            {
                return serviceLocation;
            }

            serviceLocation = RequestForServiceLocation(serviceName, cancellationToken).Result;
            _serviceLocations[serviceName] = serviceLocation;
            return serviceLocation;
        }
    }

    public void InvalidateCache()
    {
        lock (_lock)
        {
            _serviceLocations.Clear();
        }
    }
}

public static class INamingClientExtensions
{
    public const string PlaceHolder = "{0}";

    public static async Task<string> ResolveUriAsync(this INamingClient client, string uri, string serviceName,
        CancellationToken cancellationToken = default)
    {
        if (uri.Contains(PlaceHolder))
        {
            var host = await client.GetServiceLocationAsync(serviceName, cancellationToken);
            return uri.Replace(PlaceHolder, host);
        }
        return uri;
    }
}
