using NodeAgent.Utils;

namespace NodeAgent.Services;

public interface IRegisterService { }

public class RegisterService : BackgroundService, IRegisterService
{
    private ILogger _logger;
    private ISchedulerApiClient _schedulerApiClient;
    private IMonitorService _monitor;
    private IResyncFlag _resyncFlag;
    private LoopWork.StartOptions? _startOptions;

    public RegisterService(
        ILogger<RegisterService> logger,
        ISchedulerApiClient schedulerApiClient,
        IMonitorService monitor,
        IResyncFlag resyncFlag)
    {
        _logger = logger;
        _schedulerApiClient = schedulerApiClient;
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
        try
        {
            var value = _monitor.GetRegisterInfo();
            var intervalMS = await _schedulerApiClient.RegisterAsync(value, stoppingToken).ConfigureAwait(false);
            if (intervalMS > 0)
            {
                _startOptions!.IntervalSeconds = intervalMS / 1000;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when registering!");
            return false;
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
