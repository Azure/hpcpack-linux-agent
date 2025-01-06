using NodeAgent.Models;
using NodeAgent.Utils;
using System.Diagnostics;
using System.Text;

namespace NodeAgent.Services;

/*
 * NOTE
 *
 * All methods in this interface do not throw an exception. This doesn't look like a good design.
 * But as the first step to port the C++ code, let's keep it as it is in C++.
 */
public interface ITaskProcess : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task KillAsync(int forcedExitCode = 0x0FFFFFFF, bool forced = true, CancellationToken cancellationToken = default);

    Task<ProcessStatistics?> GetStatisticsFromCGroupAsync(CancellationToken cancellationToken = default);

    Task<string> PeekOutputAsync(CancellationToken cancellationToken = default);
}

//TODO: Review _messageBuffer: what to add and when. The original logic in C++ is confusing.
public class TaskProcess : ITaskProcess
{
    private static readonly char[] SpaceChars = ['\n', '\t', ' '];

    private ILogger _logger;
    private IOutputSenderFactory _outputSenderFactory;
    private IOutputSender? _outputSender;
    private ISystemService _systemService;
    private string _scriptBaseDir;
    private int _started = 0;
    private bool _ended = false;
    private int? _processId;

    private int _jobId;
    private int _taskId;
    private int _requeueCount;
    private string _cmdLine;
    private string? _stdOutFile;
    private string? _stdErrFile;
    private string? _stdInFile;
    private string? _workDir;
    private string _user;
    private bool _dumpStdOut;
    IEnumerable<ulong>? _cpuAffinity;
    IDictionary<string, string?>? _env;
    Action<int, string, ProcessStatistics>? _onComplete;

    private string _taskExecutionId;
    private bool _streamOutput;
    private string? _taskDirectory;
    private StringBuilder _messageBuffer = new StringBuilder();
    private StringBuilder? _outputBuffer;

    public int? ExitCode { get; private set; }

    public TaskProcess(
        ILogger<TaskProcess> logger,
        IOutputSenderFactory outputSenderFactory,
        ISystemService systemService,
        string scriptBaseDir,
        int jobId,
        int taskId,
        int requeueCount,
        string taskExecutionName,
        string cmdLine,
        string? stdOutFile = null,
        string? stdErrFile = null,
        string? stdInFile = null,
        string? workDir = null,
        string? user = null,
        bool dumpStdOut = false,
        IEnumerable<ulong>? cpuAffinity = null,
        IDictionary<string, string?>? env = null,
        Action<int, string, ProcessStatistics>? onComplete = null)
    {
        _logger = logger;
        _outputSenderFactory = outputSenderFactory;
        _systemService = systemService;
        _scriptBaseDir = scriptBaseDir;

        _jobId = jobId;
        _taskId = taskId;
        _requeueCount = requeueCount;
        _taskExecutionId = string.Join('_', taskExecutionName, taskId, requeueCount);
        _cmdLine = cmdLine;
        _stdOutFile = stdOutFile;
        _stdErrFile = stdErrFile;
        _stdInFile = stdInFile;
        _workDir = string.IsNullOrEmpty(_workDir) ? "~" : workDir;
        _user = user ?? "root";
        _dumpStdOut = dumpStdOut;
        _cpuAffinity = cpuAffinity;
        _env = env;
        _onComplete = onComplete;

        if (stdOutFile != null && IsHttpUrl(stdOutFile))
        {
            _streamOutput = true;
        }
    }

    private void LogError(Exception ex, string fmt, params object?[] args)
    {
        _logger.LogError(ex, _jobId, _taskId, _requeueCount, fmt, args);
    }

    private void LogWarning(Exception ex, string fmt, params object?[] args)
    {
        _logger.LogWarning(ex, _jobId, _taskId, _requeueCount, fmt, args);
    }

    private void LogWarning(string fmt, params object?[] args)
    {
        _logger.LogWarning(_jobId, _taskId, _requeueCount, fmt, args);
    }

    private void LogInformation(string fmt, params object?[] args)
    {
        _logger.LogInformation(_jobId, _taskId, _requeueCount, fmt, args);
    }

    public async ValueTask DisposeAsync()
    {
        await KillAsync().ConfigureAwait(false);
    }

    private static bool IsHttpUrl(string url)
    {
        return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    }

    private async Task CreateTaskDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var template = $"/tmp/nodemanager_task_{_taskId}_{_requeueCount}.XXXXXX";
        try
        {
            _taskDirectory = await _systemService.MakeTempDirectoryAsync(_user, template, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when creating task directory {dir}.", template);
            throw;
        }
    }

    private void NormalizeStdOutAndStdErrFiles()
    {
        Debug.Assert(!string.IsNullOrEmpty(_taskDirectory));

        if (string.IsNullOrEmpty(_stdOutFile))
        {
            _stdOutFile = Path.Join(_taskDirectory, "stdout.txt");
        }
        else if (!_stdOutFile.StartsWith('/') && !IsHttpUrl(_stdOutFile))
        {
            _stdOutFile = Path.Join(_taskDirectory, _stdOutFile);
        }

        if (string.IsNullOrEmpty(_stdErrFile))
        {
            _stdErrFile = Path.Join(_taskDirectory, "stderr.txt");
        }
        else if (!_stdErrFile.StartsWith('/') && !IsHttpUrl(_stdErrFile))
        {
            _stdErrFile = Path.Join(_taskDirectory, _stdErrFile);
        }
    }

    private async Task<string> GenerateCmdFileAsync(CancellationToken cancellationToken = default)
    {
        Debug.Assert(!string.IsNullOrEmpty(_taskDirectory));

        var path = Path.Join(_taskDirectory, "cmd.sh");
        var template = @"
#!/bin/bash

{0}
".Replace("\r\n", "\n");
        var content = string.Format(template, _cmdLine);
        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        return path;
    }

    private async Task<string> GenerateRunFileAsync(CancellationToken cancellationToken = default)
    {
        Debug.Assert(!string.IsNullOrEmpty(_taskDirectory));
        Debug.Assert(!string.IsNullOrEmpty(_workDir));
        Debug.Assert(!string.IsNullOrEmpty(_stdOutFile));
        Debug.Assert(!string.IsNullOrEmpty(_stdErrFile));

        var cmdFilePath = await GenerateCmdFileAsync(cancellationToken).ConfigureAwait(false);
        var runDirInOut = Path.Join(_taskDirectory, "run_dir_in_out.sh");

        /*
         * NOTE
         *
         * The following script comes from the C++ version and it appears some problematic,
         * and may need fix/improvement. But let's keep it as it is for now for full compatibility.
         */
        var template = @"
#!/bin/bash

cd ""{0}"" || exit $?

echo before >{1}/before1.txt 2>{1}/before2.txt || ([ ""$?"" = ""1"" ] && exit 253)

echo test >{1}/stdout.txt 2>{1}/stderr.txt || ([ ""$?"" = ""1"" ] && exit 253)

".Replace("\r\n", "\n");

        var content = string.Format(template, _workDir, _taskDirectory);

        if (_streamOutput)
        {
            content += $"/bin/bash \"{cmdFilePath}\" 2>&1";
        }
        else if (string.Equals(_stdOutFile, _stdInFile))
        {
            content += $"/bin/bash \"{cmdFilePath}\" >\"{_stdOutFile}\"  2>&1";
        }
        else
        {
            content += $"/bin/bash \"{cmdFilePath}\" >\"{_stdOutFile}\"  2>\"{_stdErrFile}\"";
        }

        if (!string.IsNullOrEmpty(_stdInFile))
        {
            content += $" <\"{_stdInFile}\"\n\n";
        }

        content += "ec=$?\n";
        content += "[ $ec -ne 0 ] && exit $ec\n\n";

        var template2 = @"
echo after >{0}/after1.txt 2>{0}/after2.txt || ([ ""$?"" = ""1"" ] && exit 253)
".Replace("\r\n", "\n");

        content += string.Format(template2, _taskDirectory);
        await File.WriteAllTextAsync(runDirInOut, content, cancellationToken).ConfigureAwait(false);
        return runDirInOut;
    }

    private bool IsDockerTask()
    {
        return _env != null && _env.TryGetValue("CCP_DOCKER_IMAGE", out var value) && !string.IsNullOrEmpty(value);
    }

    private Task PrepareDockerTaskAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private bool IsCGroupDisabled()
    {
        return _env != null && _env.TryGetValue("CCP_DISABLE_CGROUP", out var value) && string.Equals(value, "1");
    }

    private async Task DisableCGroupAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Join(_taskDirectory, "disable_cgroup");
        await File.WriteAllTextAsync(path, "1", cancellationToken).ConfigureAwait(false);
    }

    //TODO: Test it.
    public static IEnumerable<int> CalculateCpuAffinity(IEnumerable<ulong> input, int cores)
    {
        Debug.Assert(input != null);
        Debug.Assert(cores > 0);

        int coreId = 0;
        ISet<int> coreIds = new HashSet<int>();
        foreach (var num in input)
        {
            for (ulong mask = 1; mask != 0; mask <<= 1, coreId++)
            {
                if ((mask & num) != 0)
                {
                    coreIds.Add(coreId % cores);
                }
            }
        }
        return coreIds;
    }

    private async Task<string> GetCpuAffinityAsync(CancellationToken cancellationToken = default)
    {
        var cpuInfo = await _systemService.GetCpuInfoAsync(cancellationToken).ConfigureAwait(false);
        Trace.Assert(cpuInfo.Cores > 0);

        if (_cpuAffinity != null)
        {
            var aff = CalculateCpuAffinity(_cpuAffinity, cpuInfo.Cores);
            if (aff.Any())
            {
                return string.Join(',', aff);
            }
        }
        return $"0-{cpuInfo.Cores - 1}";
    }

    private async Task PrepareTaskAsync(CancellationToken cancellationToken = default)
    {
        Debug.Assert(!string.IsNullOrEmpty(_taskDirectory));

        var cpuAffinity = await GetCpuAffinityAsync(cancellationToken).ConfigureAwait(false);
        var result = await _systemService.ExecuteFileInShellAsync(
            "PrepareTask.sh", [_taskExecutionId, cpuAffinity, _taskDirectory, _user], null, _scriptBaseDir, cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new ApplicationException($"PrepareTask.sh failed: {result}");
        }
    }

    private void OnOutput(string line)
    {
        try
        {
            line += '\n';
            if (_streamOutput)
            {
                Debug.Assert(_outputSender != null);
                _outputSender.SendAsync(line).Wait();
            }
            else
            {
                Debug.Assert(_outputBuffer != null);
                _outputBuffer.Append(line);
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when sending/writing output.");
        }
    }

    private async Task StartTaskAsync(string scriptPath, CancellationToken cancellationToken = default)
    {
        Debug.Assert(!string.IsNullOrEmpty(_taskDirectory));
        Debug.Assert(!string.IsNullOrEmpty(_stdOutFile));
        Debug.Assert(_outputSender == null);

        var env = _env ?? new Dictionary<string, string?>();
        if (!env.ContainsKey("PATH"))
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            env.Add("PATH", path);
        }

        Action<string>? onStdOut = null;
        Action<string>? onStdErr = null;
        if (_streamOutput)
        {
            onStdOut = OnOutput;
            _outputSender = _outputSenderFactory.Create(_stdOutFile);
        }
        else
        {
            onStdErr = OnOutput;
            _outputBuffer = new StringBuilder();
        }

        Action<Process> onStart = (process) =>
        {
            _processId = process.Id;
        };

        //TODO/Q: Does the exit code need to go through the equivalent process of WIFEXITED and WEXITSTATUS in C++?
        ExitCode = await _systemService.ExecuteFileInShellExAsync(
            "StartTask.sh", [_taskExecutionId, scriptPath, _user, _taskDirectory], null, _scriptBaseDir, env, onStdOut, onStdErr, onStart, cancellationToken)
            .ConfigureAwait(false);

        LogInformation("Process ended with code {code}", ExitCode);

        if (_streamOutput)
        {
            await _outputSender!.SendEndAsync(cancellationToken).ConfigureAwait(false);
        }

        if (ExitCode == 0)
        {
            if (!_streamOutput)
            {
                if (_dumpStdOut)
                {
                    try
                    {
                        var result = await _systemService.ExecuteInShellAsync($"head -c 1500 \"{_stdOutFile}\"", cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                        if (result.ExitCode == 0)
                        {
                            _messageBuffer.Append($"STDOUT: {result.StdOut}");
                        }
                        else
                        {
                            _messageBuffer.AppendLine($"STDOUT: (error)");
                            LogWarning("Error when reading {file}: {result}", _stdOutFile, result);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError(ex, "Error when reading {file}", _stdOutFile);
                    }
                }

                if (!string.Equals(_stdOutFile, _stdErrFile))
                {
                    try
                    {
                        //TODO: Should it read _stdOutFile instead of _stdErrFilem, since stderr is already read and saved in _errorMsg before?
                        var result = await _systemService.ExecuteInShellAsync($"head -c 1500 \"{_stdErrFile}\"", cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                        if (result.ExitCode == 0 )
                        {
                            _messageBuffer.Append($"STDERR: {result.StdOut}");
                        }
                        else
                        {
                            _messageBuffer.AppendLine($"STDERR: (error)");
                            LogWarning("Error when reading {file}: {result}", _stdErrFile, result);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError(ex, "Error when reading {file}", _stdErrFile);
                    }
                }
            }
        }
        else
        {
            //TODO: Should it include more info like stdout and stderr?
            _messageBuffer.AppendLine($"Exit code: {ExitCode}");
        }
    }

    private async Task EndTaskAsync(CancellationToken cancellationToken = default)
    {
        Debug.Assert(ExitCode.HasValue);

        int pid = _processId ?? int.MaxValue;
        try
        {
            var result = await _systemService.ExecuteFileInShellAsync(
                "EndTask.sh", [_taskExecutionId, pid.ToString(), "1", _taskDirectory ?? string.Empty], null, _scriptBaseDir, cancellationToken)
                .ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                LogWarning("Failed ending task: {result}", result);
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when ending task.");
        }

        //GetStatisticsFromCGroupAsync doesn't throw exception.
        var stat = await GetStatisticsFromCGroupAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await _systemService.ExecuteFileInShellAsync(
                "CleanupTask.sh", [_taskExecutionId, pid.ToString(), _taskDirectory ?? string.Empty], null, _scriptBaseDir, cancellationToken)
                .ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                LogWarning("Failed cleaning up task: {result}", result);
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when cleaning up task.");
        }

        _messageBuffer.Append(_outputBuffer);
        _ended = true;

        try
        {
            _onComplete?.Invoke((int)ExitCode, _messageBuffer.ToString(), stat ?? new ProcessStatistics());
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when calling callback on completion for the task.");
        }
    }

    private async void StartInteranlAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await CreateTaskDirectoryAsync(cancellationToken).ConfigureAwait(false);
            NormalizeStdOutAndStdErrFiles();
            var filePath = await GenerateRunFileAsync(cancellationToken).ConfigureAwait(false);

            if (IsDockerTask())
            {
                await PrepareDockerTaskAsync(cancellationToken).ConfigureAwait(false);
            }
            if (IsCGroupDisabled())
            {
                await DisableCGroupAsync(cancellationToken).ConfigureAwait(false);
            }

            await PrepareTaskAsync(cancellationToken).ConfigureAwait(false);

            await StartTaskAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when starting the task.");
            ExitCode = 1;
            _messageBuffer.AppendLine(ex.Message);
        }
        await EndTaskAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _started) == 1)
        {
            StartInteranlAsync(cancellationToken);
        }
        else
        {
            LogWarning("StartAsync is called {count} times.", _started);
        }
        return Task.CompletedTask;
    }

    public async Task KillAsync(int forcedExitCode = 0x0FFFFFFF, bool forced = true, CancellationToken cancellationToken = default)
    {
        if (forcedExitCode != 0x0FFFFFFF)
        {
            ExitCode = forcedExitCode;
        }

        if (!_ended)
        {
            var pid = _processId ?? int.MaxValue;
            try
            {
                var result = await _systemService.ExecuteFileInShellAsync(
                    "EndTask.sh", [_taskExecutionId, pid.ToString(), forced ? "1" : "0", _taskDirectory ?? string.Empty], null, _scriptBaseDir, cancellationToken)
                    .ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    //TODO: Should it be debug level?
                    LogWarning("Error when killing process {pid}. Result: {result}", pid, result);
                }
            }
            catch (Exception ex)
            {
                LogError(ex, "Error when killing process {pid}.", pid);
            }
        }
    }

    private static int ParseInt(string value)
    {
        if (value.Length == 0)
        {
            return 0;
        }
        return int.Parse(value);
    }

    public static ProcessStatistics ParseStatisticsResult(string result)
    {
        var stat = new ProcessStatistics();
        var lines = result.Split('\n', 4);

        if (lines.Length != 4)
        {
            throw new FormatException($"Result is less than 4 lines:\n{result}");
        }

        stat.UserTimeMs = (ulong)(ParseInt(lines[0]) * 10);
        stat.KernelTimeMs = (ulong)(ParseInt(lines[1]) * 10);
        stat.WorkingSetKb = (ulong)(ParseInt(lines[2]) / 1024);

        var tokens = lines[3].Split(SpaceChars, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            stat.ProcessIds.Add(int.Parse(token));
        }
        return stat;
    }

    public async Task<ProcessStatistics?> GetStatisticsFromCGroupAsync(CancellationToken cancellationToken = default)
    {
        Debug.Assert(!string.IsNullOrEmpty(_taskDirectory));

        try
        {
            var result = await _systemService.ExecuteFileInShellAsync(
                "Statistics.sh", [_taskExecutionId, _taskDirectory], null, _scriptBaseDir, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                throw new ApplicationException($"Statistics.sh failed: {result}");
            }
            return ParseStatisticsResult(result.StdOut!);
        }
        catch (Exception ex)
        {
            LogWarning(ex, "Error when getting stat from CGroup.");
        }
        return null;
    }

    private async Task TailFileAsync(StringBuilder output, string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _systemService.ExecuteInShellAsync("tail", ["-c", "5000", filePath], null, cancellationToken)
                .ConfigureAwait(false);
            output.Append(result.StdOut);
            if (result.ExitCode != 0)
            {
                output.AppendLine($"Failed reading '{filePath}': {result}");
                output.Append(result.StdErr);
            }
        }
        catch (Exception ex)
        {
            LogWarning(ex, "Error when tailing file {file}.", filePath);
        }
    }

    public async Task<string> PeekOutputAsync(CancellationToken cancellationToken = default)
    {
        Debug.Assert(!string.IsNullOrEmpty(_stdOutFile));
        Debug.Assert(!string.IsNullOrEmpty(_stdErrFile));

        var stdout = new StringBuilder();
        await TailFileAsync(stdout, _stdOutFile, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(_stdOutFile, _stdErrFile))
        {
            var output = new StringBuilder();
            output.AppendLine("STDOUT:");
            output.Append(stdout);
            output.AppendLine("STDERR:");

            await TailFileAsync(output, _stdErrFile, cancellationToken).ConfigureAwait(false);
            return output.ToString();
        }
        else
        {
            return stdout.ToString();
        }
    }
}
