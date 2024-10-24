using NodeAgent.Utils;

namespace NodeAgent.Services;

public interface IRegisterService { }

public class RegisterService : BackgroundService, IRegisterService
{
    private ILogger _logger;
    private IHttpClientFactory _httpClientFactory;
    private INodeManagerConfigManager _nodeManagerConfigManager;
    private INamingClient _namingClient;
    private IMonitorService _monitor;
    private IJobTaskTable _jobTaskTable;
    private LoopWork.StartOptions? _startOptions;

    public RegisterService(ILogger<RegisterService> logger, IHttpClientFactory httpClientFactory,
        INodeManagerConfigManager configManager, INamingClient namingClient, IMonitorService monitor, IJobTaskTable jobTaskTable)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _nodeManagerConfigManager = configManager;
        _namingClient = namingClient;
        _monitor = monitor;
        _jobTaskTable = jobTaskTable;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _startOptions = new LoopWork.StartOptions
        {
            HoldSeconds = 3,
            IntervalSeconds = 300,
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
            var value = _monitor.GetRegisterInfo();
            uri = _nodeManagerConfigManager.Config.RegisterUri;
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
