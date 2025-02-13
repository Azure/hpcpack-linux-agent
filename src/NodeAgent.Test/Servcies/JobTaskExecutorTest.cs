using Microsoft.Extensions.Logging;
using NodeAgent.Models;
using NodeAgent.Services;
using NodeAgent.Test.Mocks;
using System.Runtime.Versioning;
using Xunit.Abstractions;

namespace NodeAgent.Test.Servcies;

[SupportedOSPlatform("linux")]
public class JobTaskExecutorTest : TestBase
{
    private MockSchedulerApiClientForJobTaskExecutor _schedulerApiClient;

    private ISystemService _systemService;

    private JobTaskExecutor _jobTaskExecutor;

    public JobTaskExecutorTest(ITestOutputHelper output) : base(output)
    {
        _schedulerApiClient = new MockSchedulerApiClientForJobTaskExecutor();

        var sysLogger = LoggerFactory.CreateLogger<SystemService>();
        _systemService = new SystemService(sysLogger);

        var taskFactoryLogger = LoggerFactory.CreateLogger<TaskProcessFactory>();
        var taskFactory = new TaskProcessFactory(taskFactoryLogger, LoggerFactory, _systemService);

        var logger = LoggerFactory.CreateLogger<JobTaskExecutor>();
        var flag = new ResyncFlag();

        _jobTaskExecutor = new JobTaskExecutor(logger, _schedulerApiClient, flag, _systemService, taskFactory);
    }

    [Fact]
    public async Task TestStartJobAndTaskAsync()
    {
        var args = new StartJobAndTaskArgs()
        {
            JobId = 1,
            TaskId = 2,
            StartInfo = new ProcessStartInfo() { CommandLine = "sleep 1 && hostname" },
            UserName = TestUser.RandomName,
            Password = "password",
        };
        var callbackUri = "http://callback";
        await _jobTaskExecutor.StartJobAndTaskAsync(args, callbackUri);
        var jobCount = _jobTaskExecutor.GetJobCount();
        Assert.Equal(1, jobCount);
        var taskCount = _jobTaskExecutor.GetTaskCount();
        Assert.Equal(1, taskCount);
        var taskInfo = _jobTaskExecutor.GetJobs().Single().Tasks.Single().Value;
        Assert.Equal(1, taskInfo.JobId);
        Assert.Equal(2, taskInfo.TaskId);
        Assert.Equal(0, taskInfo.TaskRequeueCount);
        Assert.False(taskInfo.Exited);

        await Task.Delay(3000);
        TestOut.OutputObject(taskInfo);

        Assert.True(taskInfo.Exited);
        Assert.Equal(0, taskInfo.ExitCode);
        Assert.Contains(_systemService.HostName, taskInfo.Message);

        Assert.Single(_schedulerApiClient.TaskCompletionCalls);
        var call = _schedulerApiClient.TaskCompletionCalls.Single();
        Assert.Equal(callbackUri, call.Uri);
        Assert.Equal(1, call.Args?.JobId);
        Assert.Equal(2, call.Args?.TaskInfo.TaskId);
        Assert.Equal(taskInfo, call.Args?.TaskInfo);

        //NOTE: Job doesn't get removed automatically after all tasks are done.
        jobCount = _jobTaskExecutor.GetJobCount();
        Assert.Equal(1, jobCount);

        taskCount = _jobTaskExecutor.GetTaskCount();
        Assert.Equal(0, taskCount);
    }
}
