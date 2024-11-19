using NodeAgent.Models;
using NodeAgent.Utils;
using System.Diagnostics;
using System.Runtime.Versioning;
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
//TODO: Review cancellationToken param for methods.
[SupportedOSPlatform("linux")]
public class TaskProcess : ITaskProcess
{
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

    private string CpuAffinity
    {
        get
        {
            throw new NotImplementedException();
        }
    }

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
        await KillAsync();
    }

    private static bool IsHttpUrl(string url)
    {
        return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    }

    private async Task CreateTaskDirectoryAsync()
    {
        var template = $"/tmp/nodemanager_task_{_taskId}_{_requeueCount}.XXXXXX";
        try
        {
            _taskDirectory = await _systemService.MakeTempDirectoryAsync(template, _user).ConfigureAwait(false);
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

    private async Task<string> GenerateCmdFileAsync()
    {
        Debug.Assert(!string.IsNullOrEmpty(_taskDirectory));

        var path = Path.Join(_taskDirectory, "cmd.sh");
        var template = @"
#!/bin/bash

{0}
".Replace("\r\n", "\n");
        var content = string.Format(template, _cmdLine);
        await File.WriteAllTextAsync(path, content).ConfigureAwait(false);
        return path;
    }

    private async Task<string> GenerateRunFileAsync()
    {
        Debug.Assert(!string.IsNullOrEmpty(_taskDirectory));
        Debug.Assert(!string.IsNullOrEmpty(_workDir));
        Debug.Assert(!string.IsNullOrEmpty(_stdOutFile));
        Debug.Assert(!string.IsNullOrEmpty(_stdErrFile));

        var cmdFilePath = await GenerateCmdFileAsync().ConfigureAwait(false);
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
        await File.WriteAllTextAsync(runDirInOut, content).ConfigureAwait(false);
        return runDirInOut;
    }

    private bool IsDockerTask()
    {
        return _env != null && _env.TryGetValue("CCP_DOCKER_IMAGE", out var value) && !string.IsNullOrEmpty(value);
    }

    private Task PrepareDockerTaskAsync()
    {
        throw new NotImplementedException();
    }

    private bool IsCGroupDisabled()
    {
        return _env != null && _env.TryGetValue("CCP_DISABLE_CGROUP", out var value) && string.Equals(value, "1");
    }

    private async Task DisableCGroupAsync()
    {
        var path = Path.Join(_taskDirectory, "disable_cgroup");
        await File.WriteAllTextAsync(path, "1").ConfigureAwait(false);
    }

    private async Task PrepareTaskAsync()
    {
        Debug.Assert(!string.IsNullOrEmpty(_taskDirectory));

        var (code, stdout, stderr) = await _systemService.ExecuteFileInShellAsync(
            "PrepareTask.sh", [_taskExecutionId, CpuAffinity, _taskDirectory, _user], workingDir: _scriptBaseDir).ConfigureAwait(false);
        if (code != 0)
        {
            throw new ApplicationException($"PrepareTask.sh failed with exit code {code}.\nStdOut:\n{stdout}\nStdErr:{stderr}");
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

    private async Task StartTaskAsync(string scriptPath)
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
            "StartTask.sh", [_taskExecutionId, scriptPath, _user, _taskDirectory], null, _scriptBaseDir, env, onStdOut, onStdErr, onStart)
            .ConfigureAwait(false);

        LogInformation("Process ended with code {code}", ExitCode);

        if (ExitCode == 0)
        {
            if (!_streamOutput)
            {
                if (_dumpStdOut)
                {
                    try
                    {
                        var (code, stdout, stderr) = await _systemService.ExecuteInShellAsync($"head -c 1500 \"{_stdOutFile}\"");
                        if (code == 0)
                        {
                            _messageBuffer.Append($"STDOUT: {stdout}");
                        }
                        else
                        {
                            _messageBuffer.AppendLine($"STDOUT: (error)");
                            LogWarning("Error when reading {file}:\nExit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}",
                                _stdOutFile, code, stdout, stderr);
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
                        var (code, stdout, stderr) = await _systemService.ExecuteInShellAsync($"head -c 1500 \"{_stdErrFile}\"");
                        if (code == 0 )
                        {
                            _messageBuffer.Append($"STDERR: {stdout}");
                        }
                        else
                        {
                            _messageBuffer.AppendLine($"STDERR: (error)");
                            LogWarning("Error when reading {file}:\nExit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}",
                                _stdErrFile, code, stdout, stderr);
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

    private async Task EndTaskAsync()
    {
        Debug.Assert(ExitCode.HasValue);

        int pid = _processId ?? int.MaxValue;
        try
        {
            var (code, stdout, stderr) = await _systemService.ExecuteFileInShellAsync(
                "EndTask.sh", [_taskExecutionId, pid.ToString(), "1", _taskDirectory ?? string.Empty], workingDir: _scriptBaseDir)
                .ConfigureAwait(false);
            if (code != 0)
            {
                LogWarning("Failed ending task.\nExit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}", code, stdout, stderr);
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when ending task.");
        }

        //GetStatisticsFromCGroupAsync doesn't throw exception.
        var stat = await GetStatisticsFromCGroupAsync().ConfigureAwait(false);

        try
        {
            var (code, stdout, stderr) = await _systemService.ExecuteFileInShellAsync(
                "CleanupTask.sh", [_taskExecutionId, pid.ToString(), _taskDirectory ?? string.Empty], workingDir: _scriptBaseDir)
                .ConfigureAwait(false);
            if (code != 0)
            {
                LogWarning("Failed cleaning up task.\nExit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}", code, stdout, stderr);
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
            await CreateTaskDirectoryAsync().ConfigureAwait(false);
            NormalizeStdOutAndStdErrFiles();
            var filePath = await GenerateRunFileAsync().ConfigureAwait(false);

            if (IsDockerTask())
            {
                await PrepareDockerTaskAsync().ConfigureAwait(false);
            }
            if (IsCGroupDisabled())
            {
                await DisableCGroupAsync().ConfigureAwait(false);
            }

            await PrepareTaskAsync().ConfigureAwait(false);

            await StartTaskAsync(filePath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when starting the task.");
            ExitCode = 1;
            _messageBuffer.AppendLine(ex.Message);
        }
        await EndTaskAsync().ConfigureAwait(false);
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
            int code = -1;
            string? stdout = null;
            string? stderr = null;
            try
            {
                (code, stdout, stderr) = await _systemService.ExecuteFileInShellAsync(
                    "EndTask.sh", [_taskExecutionId, pid.ToString(), forced ? "1" : "0", _taskDirectory ?? string.Empty], workingDir: _scriptBaseDir)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error when killing process {pid}.\nExit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}", pid, code, stdout, stderr);
            }
        }
    }

    public async Task<ProcessStatistics?> GetStatisticsFromCGroupAsync(CancellationToken cancellationToken = default)
    {
        Debug.Assert(!string.IsNullOrEmpty(_taskDirectory));

        int code = -1;
        string? stdout = null;
        string? stderr = null;
        try
        {
            (code, stdout, stderr) = await _systemService.ExecuteFileInShellAsync("Statistics.sh", [_taskExecutionId, _taskDirectory], workingDir: _scriptBaseDir);
            if (code != 0)
            {
                throw new ApplicationException($"Statistics.sh returns {code}.");
            }

            var stat = new ProcessStatistics();
            //TODO: Parse the stdout for stat...
            throw new NotImplementedException();
        }
        catch (Exception ex)
        {
            LogWarning(ex, "Error when getting stat from CGroup.\nExit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}", code, stdout, stderr);
        }
        return null;
    }

    private async Task TailFileAsync(StringBuilder output, string filePath)
    {
        int code = -1;
        string? stdout = null;
        string? stderr = null;
        try
        {
            (code, stdout, stderr) = await _systemService.ExecuteInShellAsync("tail", ["-c", "5000", filePath]);
            output.Append(stdout);
            if (code != 0)
            {
                output.AppendLine($"Failed reading '{filePath}' with exit code {code}:");
                output.Append(stderr);
            }
        }
        catch (Exception ex)
        {
            LogWarning(ex, "Error when tailing file {file}.\nExit code: {code}\nStdOut:\n{stdout}\nStdErr:\n{stderr}", filePath, code, stdout, stderr);
        }
    }

    public async Task<string> PeekOutputAsync(CancellationToken cancellationToken = default)
    {
        Debug.Assert(!string.IsNullOrEmpty(_stdOutFile));
        Debug.Assert(!string.IsNullOrEmpty(_stdErrFile));

        var stdout = new StringBuilder();
        await TailFileAsync(stdout, _stdOutFile);

        if (!string.Equals(_stdOutFile, _stdErrFile))
        {
            var output = new StringBuilder();
            output.AppendLine("STDOUT:");
            output.Append(stdout);
            output.AppendLine("STDERR:");

            await TailFileAsync(output, _stdErrFile);
            return output.ToString();
        }
        else
        {
            return stdout.ToString();
        }
    }
}
