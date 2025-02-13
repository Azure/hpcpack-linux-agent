using NodeAgent.Models;
using NodeAgent.Utils;
using System.Diagnostics;
using System.Text;
using static NodeAgent.Services.ITaskProcessFactory;

namespace NodeAgent.Services;

/*
 * NOTE
 *
 * All methods in this interface do not throw an exception. This doesn't look like a good design.
 * But as the first step to port the C++ code, let's keep it as it is in C++.
 *
 * Also note that in the original C++ version, there're only methods to start, kill and stat a task.
 * The equivalent C# methods are StartAsync, KillAsync and GetStatisticsFromCGroupAsync. You tell
 * the end of the task only by the result of GetStatisticsFromCGroupAsync. That design is not good
 * to unit test. Especially that it's not easy to tell the end of a task, and even impossible at all
 * in some situation.
 *
 * Also note:
 * ITask may be better than ITaskProcess, since there may be multiple OS processes in a task. Only
 * that Task is a name of .NET lib. So ITaskProcess and TaskProcess are used here to reduce ambiguity.
 */
public interface ITaskProcess : IAsyncDisposable
{
    //Set when StartAsync is called.
    bool IsStarted { get; }

    //Set when task ends.
    bool IsEnded { get; }

    //Set when the task is canceled or killed. But the task may not have ended yet when this is set.
    bool IsCanceled { get; }

    bool IsDockerTask { get; }

    bool IsCGroupDisabled { get; }

    //The final stat when task ends. May be null when no cgroup or an error happens when starting the task.
    ProcessStatistics? Stat { get; }

    //Start the task without waiting for its exit.
    Task StartAsync();

    //Kill the task without waiting for its exit.
    Task KillAsync(int forcedExitCode = 0x0FFFFFFF, bool forced = true);

    /*
     * NOTE
     *
     * For completeness, we may need this to wait for task end.
     *
     * Task<bool> WaitAsync(int timeout);
     */

    //The current stat of the task. May be null when no cgroup or an error happens or the task is not started or is already ended.
    Task<ProcessStatistics?> GetStatisticsFromCGroupAsync(CancellationToken cancellationToken = default);

    Task<string?> PeekOutputAsync(CancellationToken cancellationToken = default);
}

//TODO: Review _taskMessageBuffer: what to add and when. The original logic in C++ is confusing.
public class TaskProcess : ITaskProcess
{
    private static readonly char[] SpaceChars = ['\n', '\t', ' '];

    private ILogger? _logger;
    private IOutputSenderFactory? _outputSenderFactory;
    private IOutputSender? _shellOutputSender;
    private ISystemService _systemService;
    private string _scriptBaseDir;
    private int _started = 0;
    private AutoResetEvent _endable = new AutoResetEvent(true);
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
    TaskCompletionHandler? _onComplete;

    private string _taskExecutionId;
    private bool _streamOutput;
    private string? _taskDirectory;
    private StringBuilder _taskMessageBuffer = new StringBuilder();
    private StringBuilder? _shellOutputBuffer;

    private CancellationTokenSource? _cts;

    public int? ExitCode { get; private set; }

    public bool IsStarted => _started != 0;

    public bool IsEnded { get; private set; } = false;

    public bool IsCanceled => _cts != null && _cts.IsCancellationRequested;

    public bool IsDockerTask => _env != null && _env.TryGetValue("CCP_DOCKER_IMAGE", out var value) && !string.IsNullOrEmpty(value);

    public bool IsCGroupDisabled => _env != null && _env.TryGetValue("CCP_DISABLE_CGROUP", out var value) && string.Equals(value, "1");

    public ProcessStatistics? Stat { get; private set; }

    public TaskProcess(
        ILogger<TaskProcess>? logger,
        IOutputSenderFactory? outputSenderFactory,
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
        TaskCompletionHandler? onComplete = null)
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
        _workDir = workDir;
        _user = user ?? "root";
        _dumpStdOut = dumpStdOut;
        _cpuAffinity = cpuAffinity;
        _env = env;
        _onComplete = onComplete;

        if (stdOutFile != null && IsHttpUrl(stdOutFile))
        {
            _streamOutput = true;
            if (outputSenderFactory == null)
            {
                throw new ArgumentNullException(nameof(outputSenderFactory));
            }
        }
        LogDebug("A new instance is created.");
    }

    private void LogError(Exception ex, string fmt, params object?[] args)
    {
        _logger?.LogError(ex, _jobId, _taskId, _requeueCount, fmt, args);
    }

    private void LogWarning(Exception ex, string fmt, params object?[] args)
    {
        _logger?.LogWarning(ex, _jobId, _taskId, _requeueCount, fmt, args);
    }

    private void LogWarning(string fmt, params object?[] args)
    {
        _logger?.LogWarning(_jobId, _taskId, _requeueCount, fmt, args);
    }

    private void LogInformation(string fmt, params object?[] args)
    {
        _logger?.LogInformation(_jobId, _taskId, _requeueCount, fmt, args);
    }

    private void LogDebug(string fmt, params object?[] args)
    {
        _logger?.LogDebug(_jobId, _taskId, _requeueCount, fmt, args);
    }

    /*
     * NOTE
     *
     * Here _endable and _cts (if created) are not Disposed, since them may be used in EndAndCleanUpTaskAsync after KillAsync.
     */
    public async ValueTask DisposeAsync()
    {
        LogDebug("DisposeAsync");
        await KillAsync().ConfigureAwait(false);
    }

    private static bool IsHttpUrl(string url)
    {
        return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    }

    private async Task CreateTaskDirectoryAsync(CancellationToken cancellationToken)
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
        LogDebug("Task directory {dir} is created.", _taskDirectory);
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
        var template = """
#!/bin/bash

{0}
""".Replace("\r\n", "\n");
        var content = string.Format(template, _cmdLine);
        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        LogDebug("Command file {file} is created.", path);
        return path;
    }

    private async Task<string> GenerateRunFileAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(!string.IsNullOrEmpty(_taskDirectory));
        Debug.Assert(!string.IsNullOrEmpty(_stdOutFile));
        Debug.Assert(!string.IsNullOrEmpty(_stdErrFile));

        var cmdFilePath = await GenerateCmdFileAsync(cancellationToken).ConfigureAwait(false);
        var runFilePath = Path.Join(_taskDirectory, "run_dir_in_out.sh");

        /*
         * NOTE
         *
         * The following Bash script comes from the C++ version. It may need fix/improvement.
         * But let's keep it as it is for now for full compatibility.
         */

        var content = new StringBuilder();
        var workDir = string.IsNullOrEmpty(_workDir) ? "~" : _workDir;
        var template = """
#!/bin/bash

cd {0} || exit $?
echo before >{1}/before1.txt 2>{1}/before2.txt || ([ "$?" = "1" ] && exit 253)
echo test >{1}/stdout.txt 2>{1}/stderr.txt || ([ "$?" = "1" ] && exit 253)

""".Replace("\r\n", "\n");

        content.AppendFormat(template, workDir, _taskDirectory);

        if (_streamOutput)
        {
            content.Append($"""/bin/bash "{cmdFilePath}" 2>&1 """);
        }
        else if (string.Equals(_stdOutFile, _stdErrFile))
        {
            content.Append($"""/bin/bash "{cmdFilePath}" >{_stdOutFile}  2>&1 """);
        }
        else
        {
            content.Append($"""/bin/bash "{cmdFilePath}" >{_stdOutFile}  2>{_stdErrFile} """);
        }

        if (!string.IsNullOrEmpty(_stdInFile))
        {
            content.AppendLine($"""<{_stdInFile} """);
        }

        content.AppendLine();
        content.AppendLine("ec=$?");
        content.AppendLine("[ $ec -ne 0 ] && exit $ec");

        var template2 = """
echo after >{0}/after1.txt 2>{0}/after2.txt || ([ "$?" = "1" ] && exit 253)
""".Replace("\r\n", "\n");

        content.AppendFormat(template2, _taskDirectory);
        await File.WriteAllTextAsync(runFilePath, content.ToString(), cancellationToken).ConfigureAwait(false);
        LogDebug("Run file {file} is created.", runFilePath);
        return runFilePath;
    }

    private Task PrepareDockerTaskAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private async Task DisableCGroupAsync(CancellationToken cancellationToken)
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

    private async Task PrepareTaskAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(!string.IsNullOrEmpty(_taskDirectory));

        var cpuAffinity = await GetCpuAffinityAsync(cancellationToken).ConfigureAwait(false);
        var result = await _systemService.ExecuteFileInShellAsync(
            "PrepareTask.sh",
            [_taskExecutionId, cpuAffinity, _taskDirectory, _user],
            null,
            _scriptBaseDir,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new ApplicationException($"PrepareTask.sh failed: {result}");
        }
        LogDebug("Task is prepared to start.");
    }

    private void OnOutput(string line)
    {
        try
        {
            if (_streamOutput)
            {
                Debug.Assert(_shellOutputSender != null);
                _shellOutputSender.SendAsync(line + '\n').Wait();
            }
            else
            {
                Debug.Assert(_shellOutputBuffer != null);
                _shellOutputBuffer.AppendLine(line);
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when sending/writing output.");
        }
    }

    private async Task ReadFileHeadAsync(StringBuilder buffer, string filePath, bool isStdOut, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _systemService.ExecuteInShellAsync(
                $"""head -c 1500 "{filePath}" """,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var prefix = isStdOut ? "STDOUT" : "STDERR";

            if (result.ExitCode == 0)
            {
                //TODO: Use AppendLine for error proofing in case result.StdOut doesn't end with a new line.
                buffer.Append($"{prefix}: {result.StdOut}");
            }
            else
            {
                buffer.AppendLine($"{prefix}: (error)");
                LogWarning("Error when reading {file}: {result}", filePath, result);
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when reading {file}", filePath);
        }
    }

    private async Task StartTaskAsync(string scriptPath)
    {
        Debug.Assert(!string.IsNullOrEmpty(_taskDirectory));
        Debug.Assert(!string.IsNullOrEmpty(_stdOutFile));
        Debug.Assert(!string.IsNullOrEmpty(_stdErrFile));
        Debug.Assert(_shellOutputSender == null);
        Debug.Assert(_shellOutputBuffer == null);

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
            Debug.Assert(_outputSenderFactory != null);
            onStdOut = OnOutput;
            _shellOutputSender = _outputSenderFactory.Create(_stdOutFile);
        }
        else
        {
            onStdErr = OnOutput;
            _shellOutputBuffer = new StringBuilder();
        }

        Action<Process> onStart = (process) =>
        {
            _processId = process.Id;
            LogDebug("Task process Id: {pid}", _processId);
        };

        LogDebug("Start task process and wait it to exit.");

        try
        {
            /*
             * NOTE
             *
             * It should not be canceled to wait for the process exit. Or the process output would be cut off
             * before process exit on cancellation.
             */
            //TODO/Q: Does the exit code need to go through the equivalent process of WIFEXITED and WEXITSTATUS in C++?
            ExitCode = await _systemService.ExecuteFileInShellExAsync(
                "StartTask.sh",
                [_taskExecutionId, scriptPath, _user, _taskDirectory],
                null,
                _scriptBaseDir,
                env,
                onStdOut,
                onStdErr,
                onStart).ConfigureAwait(false);

            LogInformation("Task process {pid} ended with code {code}", _processId, ExitCode);
        }
        catch (Exception ex)
        {
            LogWarning(ex, "Error when running task");
            throw;
        }
        finally
        {
            /*
             * NOTE
             *
             * cancellationToken should not be applied in the finally block, since the method calls for result reporting
             * should not be canceled.
             */

            if (_streamOutput)
            {
                await _shellOutputSender!.SendEndAsync().ConfigureAwait(false);
            }

            if (ExitCode == 0)
            {
                if (!_streamOutput)
                {
                    if (_dumpStdOut)
                    {
                        await ReadFileHeadAsync(_taskMessageBuffer, _stdOutFile, true).ConfigureAwait(false);
                    }

                    if (!string.Equals(_stdOutFile, _stdErrFile))
                    {
                        await ReadFileHeadAsync(_taskMessageBuffer, _stdErrFile, false).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                _taskMessageBuffer.AppendLine($"Exit code: {ExitCode}");
            }

            //TODO: Review the logic on poping up _taskMessageBuffer, compare it with the C++ version.
            _taskMessageBuffer.Append(_shellOutputBuffer);
        }
    }

    private async Task EndTaskAsync(bool forced = false)
    {
        LogDebug("EndTaskAsync starts.");
        _endable.WaitOne();
        try
        {
            if (_taskDirectory == null)
            {
                LogDebug("Task directory is null. EndTaskAsync returns.");
                return;
            }

            //Evenv if _processId is null, we still need to call EndTask.sh, since container may run in PrepareTask.sh.
            int pid = _processId ?? int.MaxValue;
            var result = await _systemService.ExecuteFileInShellAsync(
                "EndTask.sh",
                [_taskExecutionId, pid.ToString(), forced ? "1" : "0", _taskDirectory],
                null,
                _scriptBaseDir).ConfigureAwait(true);

            if (result.ExitCode != 0)
            {
                LogWarning("Failed in ending task: {result}", result);
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when ending task.");
        }
        finally
        {
            _endable.Set();
        }
    }

    private async Task CleanUpTaskAsync()
    {
        LogDebug("CleanUpTaskAsync starts.");
        _endable.WaitOne();
        try
        {
            if (_taskDirectory == null)
            {
                LogDebug("Task directory is null. CleanUpTaskAsync returns.");
                return;
            }

            int pid = _processId ?? int.MaxValue;
            var result = await _systemService.ExecuteFileInShellAsync(
                "CleanupTask.sh",
                [_taskExecutionId, pid.ToString(), _taskDirectory],
                null,
                _scriptBaseDir).ConfigureAwait(true);

            if (result.ExitCode != 0)
            {
                LogWarning("Failed in cleaning up task: {result}", result);
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when cleaning up task.");
        }
        finally
        {
            _endable.Set();
        }
    }

    private void RemoveTaskDirectory()
    {
        LogDebug("RemoveTaskDirectory starts.");
        _endable.WaitOne();
        try
        {
            if (_taskDirectory == null)
            {
                LogDebug("Task directory is null. RemoveTaskDirectory returns.");
                return;
            }

            Directory.Delete(_taskDirectory, true);
            _taskDirectory = null;
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when removing task directory {dir}.", _taskDirectory);
        }
        finally
        {
            _endable.Set();
        }
    }

    //NOTE: This method should not throw exception.
    private async Task EndAndCleanUpTaskAsync()
    {
        LogDebug("EndAndCleanUpTaskAsync starts.");

        Debug.Assert(ExitCode.HasValue);

        if (_taskDirectory != null)
        {
            await EndTaskAsync(true).ConfigureAwait(false);

            //CleanupTask.sh will delete cgroup if any. So get stat before it.
            LogDebug("Get task statistics.");
            Stat = await GetStatisticsFromCGroupAsync().ConfigureAwait(false);

            await CleanUpTaskAsync().ConfigureAwait(false);

            if (ExitCode == 0)
            {
                RemoveTaskDirectory();
            }
        }
        else
        {
            LogDebug("Task directory is null. Skip ending and cleaning task.");
        }

        IsEnded = true;

        LogDebug("Call task completion handler.");
        try
        {
            _onComplete?.Invoke((int)ExitCode, _taskMessageBuffer.ToString(), Stat ?? new ProcessStatistics());
        }
        catch (Exception ex)
        {
            LogWarning(ex, "Error when calling task completion handler.");
        }
        LogDebug("Task is done.");
    }

    private async void StartInteranlAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CreateTaskDirectoryAsync(cancellationToken).ConfigureAwait(false);
            NormalizeStdOutAndStdErrFiles();
            var filePath = await GenerateRunFileAsync(cancellationToken).ConfigureAwait(false);

            if (IsDockerTask)
            {
                await PrepareDockerTaskAsync(cancellationToken).ConfigureAwait(false);
            }
            if (IsCGroupDisabled)
            {
                await DisableCGroupAsync(cancellationToken).ConfigureAwait(false);
            }

            await PrepareTaskAsync(cancellationToken).ConfigureAwait(false);

            //NOTE: The cancellationToken should not be passed on hereafter, that is, for StartTaskAsync and EndAndCleanUpTaskAsync.
            await StartTaskAsync(filePath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when starting task.");
            ExitCode = 1;
            _taskMessageBuffer.AppendLine(ex.ToString());
        }

        await EndAndCleanUpTaskAsync().ConfigureAwait(false);
    }

    public Task StartAsync()
    {
        if (Interlocked.Increment(ref _started) == 1)
        {
            _cts = new CancellationTokenSource();
            StartInteranlAsync(_cts.Token);
        }
        else
        {
            LogWarning("StartAsync is called {count} times.", _started);
        }
        return Task.CompletedTask;
    }

    public async Task KillAsync(int forcedExitCode = 0x0FFFFFFF, bool forced = true)
    {
        LogDebug("Kill task {force}", forced ? "forcefully" : "normally");

        //Set the exit code even when the process is not started or already ended, to behave the same way as the C++ version does.
        if (forcedExitCode != 0x0FFFFFFF)
        {
            ExitCode = forcedExitCode;
        }

        if (!IsStarted)
        {
            LogDebug("Task process is not started yet. Kill nothing.");
            return;
        }

        if (IsEnded)
        {
            LogDebug("Task process is already ended. Kill nothing.");
            return;
        }

        Debug.Assert(_cts != null);
        _cts.Cancel();

        await EndTaskAsync(forced).ConfigureAwait(false);
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
        try
        {
            if (string.IsNullOrEmpty(_taskDirectory))
            {
                throw new InvalidOperationException("Task directory is not set!");
            }

            var result = await _systemService.ExecuteFileInShellAsync(
                "Statistics.sh",
                [_taskExecutionId, _taskDirectory],
                null,
                _scriptBaseDir,
                cancellationToken).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                throw new ApplicationException($"Statistics.sh failed: {result}");
            }
            LogDebug("Statistics.sh returns:\n{out}", result.StdOut);
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
            var result = await _systemService.ExecuteInShellAsync(
                $"""tail -c 5000 "{filePath}" """, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.ExitCode == 0)
            {
                output.Append(result.StdOut);
            }
            else
            {
                output.Append(result.StdErr);
                LogWarning("Failed in tailing file '{file}': {error}", filePath, result.StdErr);
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error when tailing file {file}.", filePath);
        }
    }

    public async Task<string?> PeekOutputAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_stdOutFile) || string.IsNullOrEmpty(_stdErrFile))
            {
                throw new InvalidOperationException("Task stdout or stderr file is not set!");
            }

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
        catch (Exception ex)
        {
            LogError(ex, "Error when peeking task output. Stdout file: '{stdout}'. Stderr file: '{stderr}'", _stdOutFile, _stdErrFile);
            return null;
        }
    }
}
