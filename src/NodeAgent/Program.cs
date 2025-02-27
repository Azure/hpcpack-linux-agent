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

        builder.Services.AddHttpLogging(options => {
            options.LoggingFields = HttpLoggingFields.All;
            options.RequestHeaders.Add("CallbackUri");
        });

        builder.Services.AddControllers();

        builder.Services.ConfigureDefaultHttpClientFactory();
        builder.Services.AddTrustedCertificateCollection();

        builder.Services.AddMonitorService();
        builder.Services.AddRegisterService();
        builder.Services.AddHeartbeatService();
        //builder.Services.AddHostsManagerService();
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

        //NOTE: This has to be placed after all the other builder.Services.* calls, since its
        //implementation depends on a temporary service provider.
        builder.ConfigureKestrelServer();

        var app = builder.Build();

        app.UseHttpLogging();
        app.UseMiddleware<ErrorHandler>();
        app.UseRouting();
        app.MapControllers();

        app.Run();
    }
}
