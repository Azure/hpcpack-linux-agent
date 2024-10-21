namespace NodeAgent.Services;

public interface IMonitorService { }

public class MonitorService : BackgroundService, IMonitorService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
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
