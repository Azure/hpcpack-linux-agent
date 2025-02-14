using Microsoft.Extensions.Logging;
using NodeAgent.Models;
using NodeAgent.Services;
using NodeAgent.Test.Mocks;
using System.Runtime.Versioning;
using Xunit.Abstractions;

namespace NodeAgent.Test.Servcies;

public class IdGenerator
{
    private int _jobId = 0;

    private int _taskId = 0;

    public int JobId => ++_jobId;

    public int TaskId => ++_taskId;
}


[SupportedOSPlatform("linux")]
public class JobTaskExecutorTest : TestBase, IClassFixture<IdGenerator>
{
    private IdGenerator _idGenerator;

    private MockSchedulerApiClientForJobTaskExecutor _schedulerApiClient;

    private ISystemService _systemService;

    private JobTaskExecutor _jobTaskExecutor;

    public JobTaskExecutorTest(ITestOutputHelper output, IdGenerator idGenerator) : base(output)
    {
        _idGenerator = idGenerator;
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
        var jobId = _idGenerator.JobId;
        var taskId = _idGenerator.TaskId;
        var args = new StartJobAndTaskArgs()
        {
            JobId = jobId,
            TaskId = taskId,
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
        Assert.Equal(jobId, taskInfo.JobId);
        Assert.Equal(taskId, taskInfo.TaskId);
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
        Assert.Equal(jobId, call.Args?.JobId);
        Assert.Equal(taskId, call.Args?.TaskInfo.TaskId);
        Assert.Equal(taskInfo, call.Args?.TaskInfo);

        //NOTE: Job doesn't get removed automatically after all tasks are done.
        jobCount = _jobTaskExecutor.GetJobCount();
        Assert.Equal(1, jobCount);

        taskCount = _jobTaskExecutor.GetTaskCount();
        Assert.Equal(0, taskCount);
    }

    [Fact]
    public async Task TestStartTaskAsync()
    {
        var jobId = _idGenerator.JobId;
        //NOTE the number of elements here for the designed test.
        var taskIds = new int[] { _idGenerator.TaskId, _idGenerator.TaskId, _idGenerator.TaskId, _idGenerator.TaskId };
        var callbackUri = "http://callback";
        var jobStarted = false;
        var tasks = new Task[taskIds.Length - 1];
        var i = 0;
        foreach (var taskId in taskIds)
        {
            var cmd = $"sleep 1 && echo hellotask{taskId}";
            if (!jobStarted)
            {
                var args = new StartJobAndTaskArgs()
                {
                    JobId = jobId,
                    TaskId = taskId,
                    StartInfo = new ProcessStartInfo() { CommandLine = cmd },
                    UserName = TestUser.RandomName,
                    Password = "password",
                };
                //StartJobAndTaskAsync has to be finished before StartTaskAsync. This is by design.
                await _jobTaskExecutor.StartJobAndTaskAsync(args, callbackUri);
                jobStarted = true;
            }
            else
            {
                var args = new StartTaskArgs()
                {
                    JobId = jobId,
                    TaskId = taskId,
                    StartInfo = new ProcessStartInfo() { CommandLine = cmd },
                };
                tasks[i++] = _jobTaskExecutor.StartTaskAsync(args, callbackUri);
            }
        }
        await Task.WhenAll(tasks);

        Assert.Equal(1, _jobTaskExecutor.GetJobCount());
        Assert.Equal(taskIds.Length, _jobTaskExecutor.GetTaskCount());

        await Task.Delay(3000);

        Assert.Equal(1, _jobTaskExecutor.GetJobCount());
        Assert.Equal(0, _jobTaskExecutor.GetTaskCount());

        Assert.Equal(taskIds.Length, _schedulerApiClient.TaskCompletionCalls.Count);
        foreach (var call in _schedulerApiClient.TaskCompletionCalls)
        {
            Assert.Equal(callbackUri, call.Uri);
            Assert.NotNull(call.Args);
            Assert.Equal(jobId, call.Args.JobId);
            Assert.Contains(call.Args.TaskInfo.TaskId, taskIds);
            Assert.Contains("hellotask", call.Args.TaskInfo.Message);
        }
    }

    [Theory]
    [InlineData(3000, 3)]
    [InlineData(3000, 0)]
    [InlineData(0, 3)]
    [InlineData(0, 0)]
    public async Task TestEndTaskAsync(int delayBeforeEnd, int gracePeriod)
    {
        var jobId = _idGenerator.JobId;
        var taskId = _idGenerator.TaskId;
        var args = new StartJobAndTaskArgs()
        {
            JobId = jobId,
            TaskId = taskId,
            StartInfo = new ProcessStartInfo() { CommandLine = "sleep 100 && echo hellotask" },
            UserName = TestUser.RandomName,
            Password = "password",
        };
        var callbackUri = "http://callback";
        await _jobTaskExecutor.StartJobAndTaskAsync(args, callbackUri);

        Assert.Equal(1, _jobTaskExecutor.GetJobCount());
        Assert.Equal(1, _jobTaskExecutor.GetTaskCount());

        await Task.Delay(delayBeforeEnd);

        var endArgs = new EndTaskArgs()
        {
            JobId = jobId,
            TaskId = taskId,
            TaskCancelGracePeriodSeconds = gracePeriod
        };
        var taskInfo = await _jobTaskExecutor.EndTaskAsync(endArgs, callbackUri);
        Assert.NotNull(taskInfo);
        TestOut.OutputObject(taskInfo);

        Assert.True(taskInfo.Exited);
        Assert.NotEqual(0, taskInfo.ExitCode);
    }
}
