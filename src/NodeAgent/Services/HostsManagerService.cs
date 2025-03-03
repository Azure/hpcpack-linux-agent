using NodeAgent.Models;
using NodeAgent.Utils;
using System.Text;
using System.Text.RegularExpressions;

namespace NodeAgent.Services;

public interface IHostsManagerService { }

public class HostsManagerService : BackgroundService, IHostsManagerService
{
    public const int MinHostsFetchInterval = 30;
    public const string UpdateIdHeaderName = "UpdateId";
    public const string HostFilePath = "/etc/hosts";
    private static readonly Regex HpcHostEntryPattern = new (@"#HPC$");

    private ILogger _logger;
    private ISchedulerApiClient _schedulerApiClient;
    private IConfigManager _configManager;
    private LoopWork.StartOptions? _startOptions;
    private string? _updateId;

    public HostsManagerService(
        ILogger<HostsManagerService> logger,
        ISchedulerApiClient schedulerApiClient,
        IConfigManager configManager)
    {
        _logger = logger;
        _schedulerApiClient = schedulerApiClient;
        _configManager = configManager;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //TODO/Q: When is the service required?
        var uri = _configManager.Config.HostsFileUri;
        if (string.IsNullOrWhiteSpace(uri))
        {
            _logger.LogWarning("HostsFileUri is not specified. HostsManagerService is exiting.");
            return Task.CompletedTask;
        }

        int interval = _configManager.Config.HostsFetchInterval ?? 300;
        if (interval < MinHostsFetchInterval)
        {
            interval = MinHostsFetchInterval;
        }

        _startOptions = new LoopWork.StartOptions
        {
            HoldSeconds = 0,
            IntervalSeconds = interval,
            ErrorRetryMultiplyFactor = 1,
        };

        _logger.LogInformation("StartAsync looping with options {opts}.", _startOptions);
        return LoopWork.StartAsync(Work, OnWorkError, stoppingToken, new ChangableOptions<LoopWork.StartOptions>(_startOptions));
    }

    private async Task<bool> Work(CancellationToken stoppingToken)
    {
        try
        {
            var result = await _schedulerApiClient.GetHostsAsync(_updateId, stoppingToken).ConfigureAwait(false);
            if (result?.UpdateId != null)
            {
                _updateId = result.UpdateId;
                if (result.Hosts != null)
                {
                    await UpdateHostFileAsync(result.Hosts).ConfigureAwait(false); ;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when updating host file {file}.", HostFilePath);
            return false;
        }

        return true;
    }

    private Task OnWorkError(int _, CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }

    private async Task UpdateHostFileAsync(IEnumerable<HostEntry> hostEntries)
    {
        var content = await File.ReadAllTextAsync(HostFilePath).ConfigureAwait(false);

        _logger.LogDebug("Host file {file} before update:\n{content}", HostFilePath, content);

        var updatedContent = UpdateHostEntries(content ?? string.Empty, hostEntries);

        _logger.LogDebug("Host file {file} after update:\n{content}", HostFilePath, updatedContent);

        await File.WriteAllTextAsync(HostFilePath, updatedContent);
    }

    public static string UpdateHostEntries(string content, IEnumerable<HostEntry> hostEntries)
    {
        var lines = content.Split('\n');
        var updatedContent = new StringBuilder();

        foreach (var line in lines)
        {
            if (!HpcHostEntryPattern.IsMatch(line))
            {
                updatedContent.AppendLine(line);
            }
        }

        foreach (var entry in hostEntries)
        {
            if (!entry.HostName.Contains('.'))
            {
                updatedContent.AppendLine($"{entry.IPAddress} {entry.HostName} #HPC");
            }
        }

        //Make <NetworkType>.<NodeName> entries in the end
        foreach (var entry in hostEntries)
        {
            if (entry.HostName.Contains('.'))
            {
                updatedContent.AppendLine($"{entry.IPAddress} {entry.HostName} #HPC");
            }
        }

        return updatedContent.ToString();
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
