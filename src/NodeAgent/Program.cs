using NodeAgent.Services;

namespace NodeAgent;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();

        //TODO: Config the client ...
        builder.Services.AddHttpClient();

        builder.Services.AddMonitorService();
        builder.Services.AddRegisterService();
        builder.Services.AddHeartbeatService();
        builder.Services.AddHostsManagerService();
        builder.Services.AddMetricsService();

        builder.Services.AddSingleton<IConfigManager, ConfigManager>();
        builder.Services.AddSingleton<INamingClient, NamingClient>();
        builder.Services.AddSingleton<ISchedulerApiClient, SchedulerApiClient>();
        builder.Services.AddSingleton<IJobTaskExecutor, JobTaskExecutor>();
        builder.Services.AddSingleton<IJobTaskFilter, JobTaskFilter>();
        builder.Services.AddSingleton<IResyncFlag, ResyncFlag>();
        builder.Services.AddSingleton<ISystemService, SystemService>();
        builder.Services.AddSingleton<ITaskProcessFactory, TaskProcessFactory>();

        var app = builder.Build();

        app.UseMiddleware<ErrorHandler>();
        app.UseRouting();
        app.MapControllers();

        app.Run();
    }
}
