using Microsoft.Extensions.Logging;
using NodeAgent.Services;
using NodeAgent.Test.Mocks;
using System.Reflection;
using Xunit.Abstractions;

namespace NodeAgent.Test.Servcies;

public class TaskProcessTest : IDisposable
{
    private readonly ITestOutputHelper _output;
    private ILoggerFactory _loggerFactory;
    private ILogger<TaskProcess> _logger;
    private ISystemService _system;
    private IOutputSenderFactory _outputSenderFactory;
    private string _baseDir;

    public TaskProcessTest(ITestOutputHelper output)
    {
        _output = output;
        _loggerFactory = LoggerFactory.Create(_ => { });
        _logger = _loggerFactory.CreateLogger<TaskProcess>();
        var sysLogger = _loggerFactory.CreateLogger<SystemService>();
        _system = new SystemService(sysLogger);
        _outputSenderFactory = new MockOutputSenderFactory();
        _baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    [Fact]
    public async Task TestStartAsync()
    {
        using var user = new TestUser("testuser1", _system, _output);
        Assert.True(user.IsNew);

        var scriptBase = Path.Join(_baseDir, "Assets", "TaskProcessTest", "TestStartAsync");
        var taskProcess = new TaskProcess(
            _logger,
            _outputSenderFactory,
            _system,
            scriptBase,
            1,
            2,
            3,
            "test",
            "hostname", 
            user: user.Name);

        await taskProcess.StartAsync();
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
