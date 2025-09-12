using NodeAgent.Models;
using System.Reflection;
using System.Runtime.Versioning;
using static NodeAgent.Services.ITaskProcessFactory;

namespace NodeAgent.Services;

public interface ITaskProcessFactory
{
    delegate void TaskCompletionHandler(int exitCode, string output, ProcessStatistics? stat);

    ITaskProcess CreateProcess(
        int jobId,
        int taskId,
        int requeueCount,
        string taskExecutionName,
        string cmdLine,
        string? stdOutFile,
        string? stdErrFile,
        string? stdInFile,
        string? workDir,
        string? user,
        bool dumpStdOut,
        IEnumerable<ulong>? cpuAffinity,
        IDictionary<string, string?>? env,
        TaskCompletionHandler? onComplete);

    Task CleanupAsync(CancellationToken cancellationToken = default);

    string ScriptBaseDir { get; }
}

[SupportedOSPlatform("linux")]
public class TaskProcessFactory : ITaskProcessFactory
{
    private ILogger _logger;
    private ILoggerFactory _loggerFactory;
    private ISystemService _systemService;
    private IOutputSenderFactory? _outputSenderFactory;

    public string ScriptBaseDir { get; }

    public TaskProcessFactory(
        ILogger<TaskProcessFactory> logger,
        ILoggerFactory loggerFactory,
        ISystemService systemService,
        IOutputSenderFactory? outputSenderFactory = null,
        string? scriptBaseDir = null)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _systemService = systemService;
        _outputSenderFactory = outputSenderFactory;
        ScriptBaseDir = scriptBaseDir ?? DefaultScriptBaseDir;
        _logger.LogInformation("Script base directory: {dir}", ScriptBaseDir);
    }

    public static string DefaultScriptBaseDir
    {
        get
        {
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                return Path.GetDirectoryName(assemblyLocation)!;
            }
            return AppContext.BaseDirectory;
        }
    }

    public ITaskProcess CreateProcess(
        int jobId,
        int taskId,
        int requeueCount,
        string taskExecutionName,
        string cmdLine,
        string? stdOutFile,
        string? stdErrFile,
        string? stdInFile,
        string? workDir,
        string? user,
        bool dumpStdOut,
        IEnumerable<ulong>? cpuAffinity,
        IDictionary<string, string?>? env,
        TaskCompletionHandler? onComplete)
    {
        var logger = _loggerFactory.CreateLogger<TaskProcess>();
        return new TaskProcess(
            logger,
            _outputSenderFactory,
            _systemService,
            ScriptBaseDir,
            jobId,
            taskId,
            requeueCount,
            taskExecutionName,
            cmdLine,
            stdOutFile,
            stdErrFile,
            stdInFile,
            workDir,
            user,
            dumpStdOut,
            cpuAffinity,
            env,
            onComplete);
    }

    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _systemService.ExecuteFileInShellAsync(
                "CleanupAllTasks.sh", workingDir: ScriptBaseDir, cancellationToken: cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("CleanupAsync result: {result}", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CleanupAsync error.");
            throw;
        }
    }
}
