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
        var jobId = 1;
        var taskId = 2;
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
        var jobId = 1;
        //NOTE the number of elements here for the designed test.
        var taskIds = new int[] { 2, 3, 4, 5 };
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
}
