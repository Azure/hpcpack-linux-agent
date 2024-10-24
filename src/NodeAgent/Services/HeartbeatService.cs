using NodeAgent.Utils;

namespace NodeAgent.Services;

public interface IHeartbeatService
{
    Task PingAsync(string callbackUri);
}

public class HeartbeatService : BackgroundService, IHeartbeatService
{
    private ILogger _logger;
    private IHttpClientFactory _httpClientFactory;
    private INodeManagerConfigManager _nodeManagerConfigManager;
    private INamingClient _namingClient;
    private IJobTaskTable _jobTaskTable;
    private LoopWork.StartOptions? _startOptions;

    public HeartbeatService(ILogger<RegisterService> logger, IHttpClientFactory httpClientFactory,
        INodeManagerConfigManager configManager, INamingClient namingClient, IJobTaskTable jobTaskTable)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _nodeManagerConfigManager = configManager;
        _namingClient = namingClient;
        _jobTaskTable = jobTaskTable;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _startOptions = new LoopWork.StartOptions
        {
            HoldSeconds = 0,
            IntervalSeconds = 30,
            ErrorRetryMultiplyFactor = 2,
        };

        _logger.LogInformation("Start looping with options {opts}.", _startOptions);
        return LoopWork.StartAsync(Work, OnWorkError, stoppingToken, new ChangableOptions<LoopWork.StartOptions>(_startOptions));
    }

    private async Task<bool> Work(CancellationToken stoppingToken)
    {
        string? uri = null;
        try
        {
            var value = _jobTaskTable.GetNodeInfo();
            uri = _nodeManagerConfigManager.Config.HeartbeatUri;
            uri = _namingClient.ResolveUri(uri, _nodeManagerConfigManager.Config.DefaultServiceName, stoppingToken);

            _logger.LogDebug("Report to {uri} with {value}", uri, value);

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsJsonAsync(uri, value, stoppingToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var intervalMS = await response.Content.ReadFromJsonAsync<int>(stoppingToken).ConfigureAwait(false);
            if (intervalMS > 0)
            {
                _startOptions!.IntervalSeconds = intervalMS / 1000;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when reporting to '{uri}'!", uri);
            return false;
        }

        return true;
    }

    private Task OnWorkError(int retryCount, CancellationToken stoppingToken)
    {
        _namingClient.InvalidateCache();
        if (retryCount > 2)
        {
            _jobTaskTable.RequestResync();
        }
        return Task.CompletedTask;
    }

    public async Task PingAsync(string callbackUri)
    {
        if (callbackUri is null)
        {
            throw new ArgumentNullException(nameof(callbackUri));
        }

        var uri = _nodeManagerConfigManager.Config.HeartbeatUri;
        //TODO/Q: Should it be case-insensitive?
        if (!callbackUri.Equals(uri))
        {
            //NOTE: The operation "change and save" is not atomic by design.
            _nodeManagerConfigManager.Config.HeartbeatUri = callbackUri;
            await _nodeManagerConfigManager.SaveConfigAsync();
        }
    }
}

public static class HeartbeatServiceServiceCollectionExtensions
{
    public static IServiceCollection AddHeartbeatService(this IServiceCollection services)
    {
        services.AddSingleton<IHeartbeatService, HeartbeatService>();
        services.AddHostedService(provider => (HeartbeatService)provider.GetRequiredService<IHeartbeatService>());
        return services;
    }
}
