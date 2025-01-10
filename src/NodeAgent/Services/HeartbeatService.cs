using NodeAgent.Models;
using NodeAgent.Utils;

namespace NodeAgent.Services;

//All methods of the interface are thread-safe.
public interface IHeartbeatService
{
    Task PingAsync(string callbackUri, CancellationToken cancellationToken = default);
}

public class HeartbeatService : BackgroundService, IHeartbeatService
{
    private ILogger _logger;
    private ISchedulerApiClient _schedulerApiClient;
    private IConfigManager _configManager;
    private IJobTaskExecutor _jobTaskExecutor;
    private IResyncFlag _resyncFlag;
    private ISystemService _systemService;
    private LoopWork.StartOptions? _startOptions;

    public HeartbeatService(
        ILogger<RegisterService> logger,
        ISchedulerApiClient schedulerApiClient,
        IConfigManager configManager,
        IJobTaskExecutor jobTaskExecutor,
        IResyncFlag resyncFlag,
        ISystemService systemService)
    {
        _logger = logger;
        _schedulerApiClient = schedulerApiClient;
        _configManager = configManager;
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
        try
        {
            var _nodeInfo = new NodeInfo()
            {
                JustStarted = _resyncFlag.RequestResync,
                Jobs = _jobTaskExecutor.GetJobs(),
                Name = _systemService.HostName,
            };
            var intervalMS = await _schedulerApiClient.ReportHeartbeatAsync(_nodeInfo, stoppingToken).ConfigureAwait(false);
            if (intervalMS > 0)
            {
                _startOptions!.IntervalSeconds = intervalMS / 1000;
            }
            sent = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when reporting heartbeat!");
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
        if (retryCount > 2)
        {
            _resyncFlag.RequestResync = true;
        }
        return Task.CompletedTask;
    }

    public Task PingAsync(string callbackUri, CancellationToken cancellationToken = default)
    {
        //TODO: Use ArgumentNullException.ThrowIfNull for all such things
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
            _configManager.SaveConfig();
        }
        return Task.CompletedTask;
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
