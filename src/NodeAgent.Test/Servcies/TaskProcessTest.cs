using Microsoft.Extensions.Logging;
using NodeAgent.Models;
using NodeAgent.Services;
using System.Runtime.Versioning;
using Xunit.Abstractions;
using static NodeAgent.Services.ITaskProcessFactory;

namespace NodeAgent.Test.Servcies;

public class TaskProcessTest : TestBase
{
    private ISystemService _system;
    private string _scriptBaseDir;

    public TaskProcessTest(ITestOutputHelper output) : base(output)
    {
        var logger = LoggerFactory.CreateLogger<SystemService>();
        _system = new SystemService(logger);
        _scriptBaseDir = Path.GetDirectoryName(typeof(SystemService).Assembly.Location)!;
    }

    private bool HasRequiredLinuxCommands()
    {
        var result = _system.ExecuteInShellAsync("type pstree && type sudo").Result;
        return result.ExitCode == 0;
    }

    private TaskProcess CreateTaskProcess(string username, string cmdLine, TaskCompletionHandler? handler)
    {
        var logger = LoggerFactory.CreateLogger<TaskProcess>();
        var taskProcess = new TaskProcess(
            logger,
            null,
            _system,
            _scriptBaseDir,
            1,
            2,
            3,
            "test",
            cmdLine,
            user: username,
            dumpStdOut: true,
            onComplete: handler);
        return taskProcess;
    }

    private void OutputTaskResult(int? code, string? output, ProcessStatistics? stat)
    {
        var msg = $"""
======================================
OnComplete:
[Code]
{code}
[Stat]
{stat}
[Output]
{output}
======================================
""";
        TestOut.WriteLine(msg);
    }

    private void OutputString(string? output)
    {
        var msg = $"""
======================================
{output}
======================================
""";
        TestOut.WriteLine(msg);
    }

    [SkippableFact]
    [SupportedOSPlatform("linux")]
    public async Task TestStartTaskAsync()
    {
        Skip.IfNot(HasRequiredLinuxCommands());

        using var user = new TestUser(_system, TestOut);
        Assert.True(user.IsNew);

        var endEvent = new ManualResetEvent(false);
        int? code = null;
        string? output = null;
        ProcessStatistics? stat = null;
        TaskCompletionHandler handler = (code2, output2, stat2) => { code = code2; output = output2; stat = stat2; endEvent.Set(); };
        await using var taskProcess = CreateTaskProcess(user.Name, "hostname", handler);

        await taskProcess.StartAsync();
        Assert.True(taskProcess.IsStarted);

        Assert.True(endEvent.WaitOne(1000 * 5));
        Assert.True(taskProcess.IsEnded);
        Assert.False(taskProcess.IsCanceled);

        OutputTaskResult(code, output, stat);

        Assert.Equal(0, code);
        Assert.NotNull(output);
        Assert.NotEmpty(output);
        Assert.NotNull(stat);

        await taskProcess.KillAsync();
    }

    [SkippableTheory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(3000)]
    [SupportedOSPlatform("linux")]
    public async Task TestKillTaskAsync(int delayBeforeKill)
    {
        Skip.IfNot(HasRequiredLinuxCommands());

        using var user = new TestUser(_system, TestOut);
        Assert.True(user.IsNew);

        var endEvent = new ManualResetEvent(false);
        int? code = null;
        string? output = null;
        ProcessStatistics? stat = null;
        TaskCompletionHandler handler = (code2, output2, stat2) => { code = code2; output = output2; stat = stat2; endEvent.Set(); };
        await using var taskProcess = CreateTaskProcess(user.Name, "date -Iseconds && sleep 1000", handler);

        await taskProcess.StartAsync();
        Assert.True(taskProcess.IsStarted);

        Assert.False(endEvent.WaitOne(delayBeforeKill));
        Assert.False(taskProcess.IsEnded);

        await taskProcess.KillAsync(forced: true);
        Assert.True(taskProcess.IsCanceled);
        Assert.True(endEvent.WaitOne(2000));
        Assert.True(taskProcess.IsEnded);

        OutputTaskResult(code, output, stat);

        Assert.NotEqual(0, code);
        Assert.NotNull(output);
        Assert.NotEmpty(output);
        Assert.NotNull(stat);
    }

    [SkippableTheory]
    [InlineData(0, false)]
    [InlineData(100, false)]
    [InlineData(2000, true)]
    [SupportedOSPlatform("linux")]
    public async Task TestPeekOutputAsync(int delayBeforePeek, bool shouldHaveOutput)
    {
        Skip.IfNot(HasRequiredLinuxCommands());

        using var user = new TestUser(_system, TestOut);
        Assert.True(user.IsNew);

        await using var taskProcess = CreateTaskProcess(user.Name, "echo hello && echo world >/dev/stderr && sleep 100", null);
        await taskProcess.StartAsync();
        Assert.False(taskProcess.IsEnded);

        await Task.Delay(delayBeforePeek);
        var output = await taskProcess.PeekOutputAsync();
        OutputString(output);

        if (shouldHaveOutput)
        {
            var expected = """
STDOUT:
hello
STDERR:
world
""".Replace("\r\n", "\n");
            Assert.StartsWith(expected, output);
        }
    }

    /*
    * NOTE
    *
    * The lines of results (including the empty lines) are deliberately selected. 
    * Be careful when you change them.
    */
    [Fact]
    public void TestParseStatisticsResult()
    {
        var result =
@"


".Replace("\r\n", "\n");

        var stat = TaskProcess.ParseStatisticsResult(result);
        Assert.Equal(0ul, stat.UserTimeMs);
        Assert.Equal(0ul, stat.KernelTimeMs);
        Assert.Equal(0ul, stat.WorkingSetKb);
        Assert.Empty(stat.ProcessIds);

        result = 
@"1



".Replace("\r\n", "\n");

        stat = TaskProcess.ParseStatisticsResult(result);
        Assert.Equal(10ul, stat.UserTimeMs);
        Assert.Equal(0ul, stat.KernelTimeMs);
        Assert.Equal(0ul, stat.WorkingSetKb);
        Assert.Empty(stat.ProcessIds);

        result =
@"
1


".Replace("\r\n", "\n");

        stat = TaskProcess.ParseStatisticsResult(result);
        Assert.Equal(0ul, stat.UserTimeMs);
        Assert.Equal(10ul, stat.KernelTimeMs);
        Assert.Equal(0ul, stat.WorkingSetKb);
        Assert.Empty(stat.ProcessIds);

        result =
@"

1025

".Replace("\r\n", "\n");

        stat = TaskProcess.ParseStatisticsResult(result);
        Assert.Equal(0ul, stat.UserTimeMs);
        Assert.Equal(0ul, stat.KernelTimeMs);
        Assert.Equal(1ul, stat.WorkingSetKb);
        Assert.Empty(stat.ProcessIds);

        result =
@"


1   2

3   4


".Replace("\r\n", "\n");

        stat = TaskProcess.ParseStatisticsResult(result);
        Assert.Equal(0ul, stat.UserTimeMs);
        Assert.Equal(0ul, stat.KernelTimeMs);
        Assert.Equal(0ul, stat.WorkingSetKb);
        Assert.Equal([1, 2, 3, 4], stat.ProcessIds.ToArray());
    }

    [Fact]
    public void TestParseStatisticsResult2()
    {
        var result =
@"

".Replace("\r\n", "\n");

        Assert.Throws<FormatException>(() =>
        {
            TaskProcess.ParseStatisticsResult(result);
        });

        result =
@"x



".Replace("\r\n", "\n");

        Assert.Throws<FormatException>(() =>
        {
            TaskProcess.ParseStatisticsResult(result);
        });
    }
}
