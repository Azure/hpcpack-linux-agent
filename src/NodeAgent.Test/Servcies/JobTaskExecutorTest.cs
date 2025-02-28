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

    private const string _callbackUri = "http://callback";

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

    private async Task<(int JobId, int[] TaskIds, Task[] Tasks)> StartJobAndTasks(int numOfTasks, Func<int, int, int, string> taskCmd)
    {
        System.Diagnostics.Debug.Assert(numOfTasks > 0);

        var jobId = _idGenerator.JobId;
        var taskIds = new int[numOfTasks];
        for (var idx = 0; idx < numOfTasks; idx++)
        {
            taskIds[idx] = _idGenerator.TaskId;
        }

        var jobStarted = false;
        var tasks = new Task[taskIds.Length];
        var i = 0;
        foreach (var taskId in taskIds)
        {
            var cmd = taskCmd(i, jobId, taskId);
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
                await (tasks[i++] = _jobTaskExecutor.StartJobAndTaskAsync(args, _callbackUri));
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
                tasks[i++] = _jobTaskExecutor.StartTaskAsync(args, _callbackUri);
            }
        }
        return (jobId, taskIds, tasks);
    }

    [Fact]
    public async Task TestStartJobAndTaskAsync()
    {
        var (jobId, taskIds, tasks) = await StartJobAndTasks(1, (_, _, _) => "sleep 1 && hostname");
        var taskId = taskIds[0];

        var jobCount = _jobTaskExecutor.GetJobCount();
        Assert.Equal(1, jobCount);
        var taskCount = _jobTaskExecutor.GetTaskCount();
        Assert.Equal(1, taskCount);
        var taskInfo = _jobTaskExecutor.GetJobs().Single().Tasks.Single();
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
        Assert.Equal(_callbackUri, call.Uri);
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
        //NOTE the number of elements here for the designed test.
        var (jobId, taskIds, tasks) = await StartJobAndTasks(4, (_, _, taskId) => $"sleep 1 && echo hellotask{taskId}");
        await Task.WhenAll(tasks);

        Assert.Equal(1, _jobTaskExecutor.GetJobCount());
        Assert.Equal(taskIds.Length, _jobTaskExecutor.GetTaskCount());

        await Task.Delay(3000);

        Assert.Equal(1, _jobTaskExecutor.GetJobCount());
        Assert.Equal(0, _jobTaskExecutor.GetTaskCount());

        Assert.Equal(taskIds.Length, _schedulerApiClient.TaskCompletionCalls.Count);
        foreach (var call in _schedulerApiClient.TaskCompletionCalls)
        {
            Assert.Equal(_callbackUri, call.Uri);
            Assert.NotNull(call.Args);
            Assert.Equal(jobId, call.Args.JobId);
            Assert.Contains(call.Args.TaskInfo.TaskId, taskIds);
            Assert.Contains("hellotask", call.Args.TaskInfo.Message);
        }
    }

    [Theory]
    [InlineData(3, 3, 10, false)]
    [InlineData(3, 0, 10, false)]
    [InlineData(0, 3, 10, false)]
    [InlineData(0, 0, 10, false)]
    [InlineData(3, 0, 0, true)]
    [InlineData(3, 3, 0, true)]
    public async Task TestEndTaskAsync(int delay, int gracePeriod, int sleep, bool endEarly)
    {
        var (jobId, taskIds, tasks) = await StartJobAndTasks(1, (_, _, _) => $"sleep {sleep} && echo hellotask");
        var taskId = taskIds[0];

        Assert.Equal(1, _jobTaskExecutor.GetJobCount());
        Assert.Equal(1, _jobTaskExecutor.GetTaskCount());

        await Task.Delay(delay * 1000);

        var endArgs = new EndTaskArgs()
        {
            JobId = jobId,
            TaskId = taskId,
            TaskCancelGracePeriodSeconds = gracePeriod
        };
        var taskInfo = await _jobTaskExecutor.EndTaskAsync(endArgs, _callbackUri);
        if (endEarly)
        {
            Assert.Null(taskInfo);
        }
        else
        {
            Assert.NotNull(taskInfo);
            TestOut.OutputObject(taskInfo);
            Assert.True(taskInfo.Exited);
            Assert.NotEqual(0, taskInfo.ExitCode);
        }

        Assert.Single(_schedulerApiClient.TaskCompletionCalls);
        var call = _schedulerApiClient.TaskCompletionCalls.Single();
        Assert.Equal(_callbackUri, call.Uri);
        Assert.NotNull(call.Args);
        Assert.Equal(call.Args.JobId, endArgs.JobId);
        Assert.Equal(call.Args.TaskInfo.TaskId, endArgs.TaskId);
    }

    //TODO: Test end multiple tasks concurrently

    [Fact]
    public async Task TestEndJobAsync()
    {
        var (jobId, taskIds, tasks) = await StartJobAndTasks(10, (_, _, taskId) => $"sleep 100 && echo hellotask{taskId}");
        await Task.WhenAll(tasks);

        Assert.Equal(1, _jobTaskExecutor.GetJobCount());
        Assert.Equal(taskIds.Length, _jobTaskExecutor.GetTaskCount());

        var args = new EndJobArgs() { JobId = jobId };
        var jobInfo = await _jobTaskExecutor.EndJobAsync(args);
        Assert.NotNull(jobInfo);
        TestOut.OutputObject(jobInfo);

        Assert.Equal(jobId, jobInfo.JobId);
        Assert.Equal(10, jobInfo.Tasks.Count());

        Assert.Equal(0, _jobTaskExecutor.GetJobCount());
        Assert.Equal(0, _jobTaskExecutor.GetTaskCount());

        Assert.Empty(_schedulerApiClient.TaskCompletionCalls);
    }

    [Theory]
    [InlineData(0, 3, false)]
    [InlineData(5, 3, true)]
    public async Task TestPeekTaskOutputAsync(int sleep, int delay, bool hasOutput)
    {
        var (jobId, taskIds, tasks) = await StartJobAndTasks(1, (_, _, _) => $"echo hellotask && sleep {sleep}");
        await Task.Delay(delay * 1000);

        var args = new PeekTaskOutputArgs() { JobId = jobId, TaskId = taskIds[0] };
        var result = await _jobTaskExecutor.PeekTaskOutputAsync(args);
        if (hasOutput)
        {
            TestOut.OutputString(result);
            Assert.Contains("hellotask", result);
        }
        else
        {
            Assert.Null(result);
        }
    }
}
