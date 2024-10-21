namespace NodeAgent.Services;

public interface IHeartbeatService { }

public class HeartbeatService : BackgroundService, IHeartbeatService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
