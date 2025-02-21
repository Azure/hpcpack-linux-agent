using NodeAgent.Models;

namespace NodeAgent.Services;

//All methods of the interface are thread-safe.
public interface IMonitorService
{
    Task<RegisterInfo> GetRegisterInfoAsync();
}

public class MonitorService : BackgroundService, IMonitorService
{
    ILogger _logger;
    ISystemService _systemService;

    public MonitorService(ILogger<MonitorService> logger, ISystemService systemService)
    {
        _logger = logger;
        _systemService = systemService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }

    public async Task<RegisterInfo> GetRegisterInfoAsync()
    {
        var info = new RegisterInfo()
        {
            NodeName = _systemService.HostName
        };
        var tasks = new List<Task>();

        tasks.Add(Task.Run(async () => {
            var cpuInfo = await _systemService.GetCpuInfoAsync().ConfigureAwait(false);
            info.CoreCount = cpuInfo.Cores;
            info.SocketCount = cpuInfo.Cores;
        }));
        tasks.Add(Task.Run(async () =>
        {
            var memInfo = await _systemService.GetMemoryUsageAsync().ConfigureAwait(false);
            info.MemoryMegabytes = memInfo.Total / 1024;
        }));
        tasks.Add(Task.Run(async () =>
        {
            var distro = await _systemService.GetDistroInfoAsync().ConfigureAwait(false);
            info.DistroInfo = distro;
        }));
        tasks.Add(Task.Run(async () =>
        {
            try
            {
                var gpuInfo = await _systemService.GetGpuInfoAsync().ConfigureAwait(false);
                info.GpuInfo = gpuInfo;
            }
            catch (SystemException ex)
            {
                _logger.LogWarning(ex, "Error when getting GPU info.");
            }
        }));
        tasks.Add(Task.Run(() =>
        {
            var networkInfo = _systemService.GetNetworkInfo();
            info.NetworksInfo = networkInfo;
        }));

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when getting system information.");
            throw;
        }
        return info;
    }
}

public static class MonitorServiceServiceCollectionExtensions
{
    public static IServiceCollection AddMonitorService(this IServiceCollection services)
    {
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddHostedService(provider => (MonitorService)provider.GetRequiredService<IMonitorService>());
        return services;
    }
}
