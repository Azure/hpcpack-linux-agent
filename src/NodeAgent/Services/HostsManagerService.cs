using NodeAgent.Models;
using NodeAgent.Utils;
using System.Net;

namespace NodeAgent.Services;

public interface IHostsManagerService { }

public class HostsManagerService : BackgroundService, IHostsManagerService
{
    public const int MinHostsFetchInterval = 30;
    public const string UpdateIdHeaderName = "UpdateId";

    private ILogger _logger;
    private IHttpClientFactory _httpClientFactory;
    private INodeManagerConfigManager _nodeManagerConfigManager;
    private INamingClient _namingClient;
    private LoopWork.StartOptions? _startOptions;
    private string? _updateId;

    public HostsManagerService(ILogger<RegisterService> logger, IHttpClientFactory httpClientFactory,
        INodeManagerConfigManager configManager, INamingClient namingClient)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _nodeManagerConfigManager = configManager;
        _namingClient = namingClient;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //TODO/Q: When is the service required?
        var uri = _nodeManagerConfigManager.Config.HostsFileUri;
        if (string.IsNullOrWhiteSpace(uri))
        {
            _logger.LogWarning("HostsFileUri is not specified. HostsManagerService is exiting.");
            return Task.CompletedTask;
        }

        int interval = _nodeManagerConfigManager.Config.HostsFetchInterval ?? 300;
        if (interval < MinHostsFetchInterval)
        {
            interval = MinHostsFetchInterval;
        }

        _startOptions = new LoopWork.StartOptions
        {
            HoldSeconds = 0,
            IntervalSeconds = interval,
            ErrorRetryMultiplyFactor = 1,
        };

        _logger.LogInformation("StartAsync looping with options {opts}.", _startOptions);
        return LoopWork.StartAsync(Work, OnWorkError, stoppingToken, new ChangableOptions<LoopWork.StartOptions>(_startOptions));
    }

    private async Task<bool> Work(CancellationToken stoppingToken)
    {
        string? uri = null;
        try
        {
            uri = _nodeManagerConfigManager.Config.HostsFileUri;
            uri = await _namingClient.ResolveUriAsync(uri!, _nodeManagerConfigManager.Config.DefaultServiceName, stoppingToken);

            _logger.LogDebug("Request to {uri}", uri);

            var httpClient = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (!string.IsNullOrEmpty(_updateId))
            {
                request.Headers.Add(UpdateIdHeaderName, _updateId);
            }

            var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                _logger.LogInformation("No update from server.");
                return true;
            }

            var values= response.Headers.GetValues(UpdateIdHeaderName);
            _updateId = values.First();
            _logger.LogInformation("Received update id {id}", _updateId);

            var hostEntries = await response.Content.ReadFromJsonAsync<IEnumerable<HostEntry>>(stoppingToken).ConfigureAwait(false);
            UpdateHostsFile(hostEntries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting update from '{uri}'!", uri);
            return false;
        }

        return true;
    }

    private Task OnWorkError(int _, CancellationToken stoppingToken)
    {
        _namingClient.InvalidateCache();
        return Task.CompletedTask;
    }

    private void UpdateHostsFile(IEnumerable<HostEntry>? hostEntries)
    {
        throw new NotImplementedException();
    }
}

public static class HostsManagerServiceServiceCollectionExtensions
{
    public static IServiceCollection AddHostsManagerService(this IServiceCollection services)
    {
        services.AddSingleton<IHostsManagerService, HostsManagerService>();
        services.AddHostedService(provider => (HostsManagerService)provider.GetRequiredService<IHostsManagerService>());
        return services;
    }
}
