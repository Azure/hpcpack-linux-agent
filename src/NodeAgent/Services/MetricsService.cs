namespace NodeAgent.Services;

public interface IMetricsService { }

public class MetricsService : BackgroundService, IMetricsService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}

public static class MetricsServiceServiceCollectionExtensions
{
    public static IServiceCollection AddMetricsService(this IServiceCollection services)
    {
        services.AddSingleton<IMetricsService, MetricsService>();
        services.AddHostedService(provider => (MetricsService)provider.GetRequiredService<IMetricsService>());
        return services;
    }
}
