using NodeAgent.Models;
using System.Reflection;
using System.Runtime.Versioning;

namespace NodeAgent.Services;

public interface ITaskProcessFactory
{
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
        Action<int, string, ProcessStatistics>? onComplete);

    Task CleanupAsync(CancellationToken cancellationToken);
}

[SupportedOSPlatform("linux")]
public class TaskProcessFactory : ITaskProcessFactory
{
    private ILogger _logger;
    private ILoggerFactory _loggerFactory;
    private ISystemService _systemService;
    private IOutputSenderFactory _outputSenderFactory;
    private string _scriptBaseDir;

    public TaskProcessFactory(
        ILogger<TaskProcessFactory> logger,
        ILoggerFactory loggerFactory,
        ISystemService systemService,
        IOutputSenderFactory outputSenderFactory,
        string? scriptBaseDir = null)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _systemService = systemService;
        _outputSenderFactory = outputSenderFactory;
        _scriptBaseDir = scriptBaseDir ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        _logger.LogInformation("Script base directory: {dir}", _scriptBaseDir);
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
        Action<int, string, ProcessStatistics>? onComplete)
    {
        var logger = _loggerFactory.CreateLogger<TaskProcess>();
        return new TaskProcess(
            logger,
            _outputSenderFactory,
            _systemService,
            _scriptBaseDir,
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

    public async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var (code, stdout, stderr) = await _systemService.ExecuteFileInShellAsync(
            "CleanupAllTasks.sh", workingDir: _scriptBaseDir, cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("CleanupAsync result:\nExit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}", code, stdout, stderr);
    }
}
