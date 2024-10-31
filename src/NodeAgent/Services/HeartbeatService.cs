using NodeAgent.Models;
using NodeAgent.Utils;

namespace NodeAgent.Services;

//All methods of the interface are thread-safe.
public interface IHeartbeatService
{
    Task PingAsync(string callbackUri);
}

public class HeartbeatService : BackgroundService, IHeartbeatService
{
    private ILogger _logger;
    private IHttpClientFactory _httpClientFactory;
    private IConfigManager _configManager;
    private INamingClient _namingClient;
    private IJobTaskExecutor _jobTaskExecutor;
    private IResyncFlag _resyncFlag;
    private ISystemService _systemService;
    private LoopWork.StartOptions? _startOptions;

    public HeartbeatService(
        ILogger<RegisterService> logger,
        IHttpClientFactory httpClientFactory,
        IConfigManager configManager,
        INamingClient namingClient,
        IJobTaskExecutor jobTaskExecutor,
        IResyncFlag resyncFlag,
        ISystemService systemService)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configManager = configManager;
        _namingClient = namingClient;
        _jobTaskExecutor = jobTaskExecutor;
        _resyncFlag = resyncFlag;
        _systemService = systemService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _startOptions = new LoopWork.StartOptions
        {
            HoldSeconds = 0,
            IntervalSeconds = 30,
            ErrorRetryMultiplyFactor = 2,
        };

        _logger.LogInformation("StartAsync looping with options {opts}.", _startOptions);
        return LoopWork.StartAsync(Work, OnWorkError, stoppingToken, new ChangableOptions<LoopWork.StartOptions>(_startOptions));
    }

    private async Task<bool> Work(CancellationToken stoppingToken)
    {
        bool sent = false;
        string? uri = null;
        try
        {
            var _nodeInfo = new NodeInfo()
            {
                JustStarted = _resyncFlag.RequestResync,
                Jobs = _jobTaskExecutor.GetJobs(),
                Name = _systemService.HostName,
            };

            uri = _configManager.Config.HeartbeatUri;
            uri = await _namingClient.ResolveUriAsync(uri, _configManager.Config.DefaultServiceName, stoppingToken);

            _logger.LogDebug("Report to {uri} with {value}", uri, _nodeInfo);

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsJsonAsync(uri, _nodeInfo, stoppingToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            sent = true;

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
        finally
        {
            if (sent)
            {
                _resyncFlag.RequestResync = false;
            }
        }

        return true;
    }

    private Task OnWorkError(int retryCount, CancellationToken stoppingToken)
    {
        _namingClient.InvalidateCache();
        if (retryCount > 2)
        {
            _resyncFlag.RequestResync = true;
        }
        return Task.CompletedTask;
    }

    public async Task PingAsync(string callbackUri)
    {
        if (callbackUri is null)
        {
            throw new ArgumentNullException(nameof(callbackUri));
        }

        var uri = _configManager.Config.HeartbeatUri;
        //TODO/Q: Should it be case-insensitive?
        if (!callbackUri.Equals(uri))
        {
            //NOTE: The operation "change and save" is not atomic by design.
            _configManager.Config.HeartbeatUri = callbackUri;
            await _configManager.SaveConfigAsync();
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
