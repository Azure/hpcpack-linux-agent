using Microsoft.AspNetCore.HttpLogging;
using NodeAgent.Services;
using NodeAgent.Services.Extensions;
using NReco.Logging.File;
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

        builder.Services.AddLogging(loggingBuilder => {
            var loggingSection = builder.Configuration.GetSection("Logging");
            loggingBuilder.AddFile(loggingSection);
        });

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

        /*
         * NOTE:
         *
         * When a new instance of the app is started, while an existing one is running, the cleanup procedure
         * may break/fail running tasks in the latter. So it would be nice to keep the app singleton in some way.
         */
        var processFactory = app.Services.GetRequiredService<ITaskProcessFactory>();
        processFactory.CleanupAsync().Wait();

        app.UseHttpLogging();
        app.UseMiddleware<ErrorHandler>();
        app.UseRouting();
        app.MapControllers();

        app.Run();
    }
}
