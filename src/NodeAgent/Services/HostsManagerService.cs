namespace NodeAgent.Services;

public interface IHostsManagerService { }

public class HostsManagerService : BackgroundService, IHostsManagerService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
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
