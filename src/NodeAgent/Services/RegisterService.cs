using NodeAgent.Utils;

namespace NodeAgent.Services;

public interface IRegisterService { }

public class RegisterService : BackgroundService, IRegisterService
{
    private ILogger _logger;
    private IHttpClientFactory _httpClientFactory;
    private IConfigManager _configManager;
    private INamingClient _namingClient;
    private IMonitorService _monitor;
    private IResyncFlag _resyncFlag;
    private LoopWork.StartOptions? _startOptions;

    public RegisterService(
        ILogger<RegisterService> logger,
        IHttpClientFactory httpClientFactory,
        IConfigManager configManager,
        INamingClient namingClient,
        IMonitorService monitor,
        IResyncFlag resyncFlag)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configManager = configManager;
        _namingClient = namingClient;
        _monitor = monitor;
        _resyncFlag = resyncFlag;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _startOptions = new LoopWork.StartOptions
        {
            HoldSeconds = 3,
            IntervalSeconds = 300,
            ErrorRetryMultiplyFactor = 2,
        };

        _logger.LogInformation("StartAsync looping with options {opts}.", _startOptions);
        return LoopWork.StartAsync(Work, OnWorkError, stoppingToken, new ChangableOptions<LoopWork.StartOptions>(_startOptions));
    }

    private async Task<bool> Work(CancellationToken stoppingToken)
    {
        string? uri = null;
        try
        {
            var value = _monitor.GetRegisterInfo();
            uri = _configManager.Config.RegisterUri;
            uri = await _namingClient.ResolveUriAsync(uri, _configManager.Config.DefaultServiceName, stoppingToken);

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
            _resyncFlag.RequestResync = true;
        }
        return Task.CompletedTask;
    }
}

public static class RegisterServiceServiceCollectionExtensions
{
    public static IServiceCollection AddRegisterService(this IServiceCollection services)
    {
        services.AddSingleton<IRegisterService, RegisterService>();
        services.AddHostedService(provider => (RegisterService)provider.GetRequiredService<IRegisterService>());
        return services;
    }
}
