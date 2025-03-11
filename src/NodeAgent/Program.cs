using Microsoft.AspNetCore.HttpLogging;
using NodeAgent.Services;
using NodeAgent.Services.Extensions;
using System.Runtime.Versioning;

namespace NodeAgent;

public class Program
{
    [SupportedOSPlatform("linux")]
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var trustedCAStore = builder.Configuration.GetTrustedCAStore();

        builder.Services.ConfigureKestrelServer(trustedCAStore);
        builder.Services.ConfigureDefaultHttpClientFactory(trustedCAStore);

        builder.Services.AddHttpLogging(options => {
            options.LoggingFields = HttpLoggingFields.All;
            options.RequestHeaders.Add("CallbackUri");
        });

        builder.Services.AddControllers();

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
        builder.Services.AddSingleton<IOutputSenderFactory, OutputSenderFactory>();

        var app = builder.Build();

        app.UseHttpLogging();
        app.UseMiddleware<ErrorHandler>();
        app.UseRouting();
        app.MapControllers();

        app.Run();
    }
}
