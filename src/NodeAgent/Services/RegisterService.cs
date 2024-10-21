
namespace NodeAgent.Services;

public interface IRegisterService { }

public class RegisterService : BackgroundService, IRegisterService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
