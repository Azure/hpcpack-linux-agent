using NodeAgent.Models;
using NodeAgent.Utils;

namespace NodeAgent.Services;

public interface IHostsManagerService { }

public class HostsManagerService : BackgroundService, IHostsManagerService
{
    public const int MinHostsFetchInterval = 30;
    public const string UpdateIdHeaderName = "UpdateId";

    private ILogger _logger;
    private ISchedulerApiClient _schedulerApiClient;
    private IConfigManager _configManager;
    private LoopWork.StartOptions? _startOptions;
    private string? _updateId;

    public HostsManagerService(
        ILogger<HostsManagerService> logger,
        ISchedulerApiClient schedulerApiClient,
        IConfigManager configManager)
    {
        _logger = logger;
        _schedulerApiClient = schedulerApiClient;
        _configManager = configManager;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //TODO/Q: When is the service required?
        var uri = _configManager.Config.HostsFileUri;
        if (string.IsNullOrWhiteSpace(uri))
        {
            _logger.LogWarning("HostsFileUri is not specified. HostsManagerService is exiting.");
            return Task.CompletedTask;
        }

        int interval = _configManager.Config.HostsFetchInterval ?? 300;
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
            var result = await _schedulerApiClient.GetHostsAsync(_updateId, stoppingToken).ConfigureAwait(false);
            if (result?.UpdateId != null)
            {
                _updateId = result.UpdateId;
                UpdateHostsFile(result.Hosts);
            }
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
