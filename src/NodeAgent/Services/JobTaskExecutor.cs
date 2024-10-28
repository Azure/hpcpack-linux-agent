using NodeAgent.Models;
using NodeAgent.Utils;

namespace NodeAgent.Services;

//All methods of the interface are thread-safe.
public interface IJobTaskExecutor
{
    Task StartJobAndTaskAsync(StartJobAndTaskArgs args, string callbackUri);

    Task StartTaskAsync(StartTaskArgs args, string callbackUri);

    Task<TaskInfo?> EndTaskAsync(EndTaskArgs args, string callbackUri);

    Task<JobInfo?> EndJobAsync(EndJobArgs args);

    Task<string> PeekTaskOutputAsync(PeekTaskOutputArgs args);

    int GetJobCount();

    int GetTaskCount();

    int GetCoresInUse();

    IEnumerable<JobInfo> GetJobs();
}

/*
 * TODO
 * 
 * Review locks in JobTaskExecutor and JobTaskTable for thread-safety, deadlock and performance.
 */
public class JobTaskExecutor : IJobTaskExecutor
{
    private ILogger _logger;
    private INamingClient _namingClient;
    private INodeManagerConfigManager _configManager;
    private IHttpClientFactory _httpClientFactory;
    private IResyncFlag _resyncFlag;
    private JobTaskTable _jobTaskTable = new JobTaskTable();
    private IDictionary<int, UserInfo> _jobUsers = new Dictionary<int, UserInfo>();
    private IDictionary<string, ISet<int>> _userJobs = new Dictionary<string, ISet<int>>();
    private IDictionary<ulong, Process> _processes = new Dictionary<ulong, Process>();
    private object _lock = new object();

    public JobTaskExecutor(ILogger<JobTaskExecutor> logger, INamingClient namingClient,
        INodeManagerConfigManager configManager, IHttpClientFactory httpClientFactory, IResyncFlag resyncFlag)
    {
        _logger = logger;
        _namingClient = namingClient;
        _configManager = configManager;
        _httpClientFactory = httpClientFactory;
        _resyncFlag = resyncFlag;
    }

    private void Log(LogLevel level, int jobId, int? TaskId, int? requeue, string fmt, params object?[] args)
    {
        if (_logger.IsEnabled(level))
        {
            var msg = $"Job '{jobId}', Task '{TaskId}.{requeue}': {fmt}";
            _logger.Log(level, msg, args);
        }
    }

    private void LogError(Exception ex, int jobId, int? TaskId, int? requeue, string fmt, params object?[] args)
    {
        if (_logger.IsEnabled(LogLevel.Error))
        {
            var msg = $"Job '{jobId}', Task '{TaskId}.{requeue}': {fmt}";
            _logger.LogError(ex, msg, args);
        }
    }

    private UserInfo SetupUserAccount(StartJobAndTaskArgs args)
    {
        throw new NotImplementedException();
    }

    private void CleanupUserAccount(UserInfo userInfo)
    {
        throw new NotImplementedException();
    }

    //TODO: Force task yield to unblock thread that is awaiting it?
    public Task StartJobAndTaskAsync(StartJobAndTaskArgs args, string callbackUri)
    {
        lock (_lock)
        {
            var user = SetupUserAccount(args);
            var userName = user.Item1;

            var added = _jobUsers.TryAdd(args.JobId, user);
            Log(LogLevel.Debug, args.JobId, args.TaskId, null, "User '{user}' is added to jobUsers table.", userName);

            var hasValue = _userJobs.TryGetValue(userName, out var jobs);
            if (hasValue)
            {
                jobs!.Add(args.JobId);
            }
            else
            {
                jobs = new HashSet<int>() { args.JobId };
                _userJobs.Add(userName, jobs);
            }
        }
        return StartTaskAsync(args.ToStartTaskArgs(), callbackUri);
    }

    public Task StartTaskAsync(StartTaskArgs args, string callbackUri)
    {
        lock (_lock)
        {
            var hasValue = _jobUsers.TryGetValue(args.JobId, out var user);
            if (!hasValue)
            {
                throw new InvalidOperationException($"Job {args.JobId} was not started on this node.");
            }

            var userName = user!.Item1;
            var taskInfo = _jobTaskTable.AddJobAndTask(args.JobId, args.TaskId, out var isNewEntry);
            taskInfo.Affinity = args.StartInfo.Affinity;
            taskInfo.TaskRequeueCount = args.StartInfo.TaskRequeueCount;

            if (string.IsNullOrEmpty(args.StartInfo.CommandLine))
            {
                Log(LogLevel.Information, taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount,
                    "MPI non-master task found, skip creating the process.");

                return Task.FromException(new NotImplementedException());
            }
            else
            {
                if (!isNewEntry || _processes.ContainsKey(taskInfo.ProcessKey))
                {
                    Log(LogLevel.Warning, taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount, "The task has already started.");
                }
                else
                {
                    //Let the following lambda capture the copy instead of the original object.
                    var taskInfoCopy = taskInfo.Copy();

                    var process = new Process(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount, "Task",
                        args.StartInfo.CommandLine, args.StartInfo.StdOutFile, args.StartInfo.StdErrFile, args.StartInfo.StdInFile,
                        args.StartInfo.WorkDirectory, userName, true, args.StartInfo.Affinity, args.StartInfo.EnvironmentVariables,
                        (exitCode, message, stat) =>
                        {
                            System.Diagnostics.Debug.Assert(!taskInfo.Exited, "Task already exited.");
                            OnTaskProcessComplete(taskInfoCopy, callbackUri, exitCode, message, stat);
                        });

                    _processes[taskInfo.ProcessKey] = process;

                    Log(LogLevel.Debug, taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount, 
                        "Start process with ProcessKey {key} and process count {count}", taskInfo.ProcessKey, _processes.Count);

                    return process.StartAsync().ContinueWith(task => {
                        if (task.IsCompletedSuccessfully)
                        {
                            var (pid, tid) = task.Result;
                            Log(LogLevel.Debug, taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount,
                                "Process started with pid {0} and tid {1}", pid, tid);
                        }
                        else
                        {
                            LogError(task.Exception!, taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount, "Failed starting process.");
                        }
                    });
                }
            }
            return Task.CompletedTask;
        }
    }

    private void OnTaskProcessComplete(TaskInfo taskInfo, string callbackUri, int processExitCode, string processMessage, ProcessStatistics stat)
    {
        taskInfo.CancelGracefulPeriod?.Cancel();
        taskInfo.Exited = true;
        taskInfo.ExitCode = processExitCode;
        taskInfo.Message = processMessage;
        taskInfo.AssignFromStat(stat);

        //TODO: Fire and forget?
        ReportTaskCompletionAsync(taskInfo.ToTaskCompletionEventArgs(), callbackUri).Wait();

        lock (_lock)
        {
            //This won't remove the task entry added later as attempt id doesn't match
            _jobTaskTable.RemoveTask(taskInfo.JobId, taskInfo.TaskId, taskInfo.AttemptId);

            Log(LogLevel.Debug, taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount,
                "Remove task process: ProcessKey {key}, AttemptId {id}", taskInfo.ProcessKey, taskInfo.AttemptId);

            _processes.Remove(taskInfo.ProcessKey);
        }
    }

    private async Task ReportTaskCompletionAsync(TaskCompletionEventArgs args, string uri, CancellationToken cancelToken = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(_configManager.Config.TaskCompletionUri))
            {
                uri = _configManager.Config.TaskCompletionUri;
            }
            uri = await _namingClient.ResolveUriAsync(uri, _configManager.Config.DefaultServiceName, cancelToken);
            Log(LogLevel.Debug, args.JobId, args.TaskInfo.TaskId, args.TaskInfo.TaskRequeueCount,
                "Report task completion to {uri} with {args}", uri, args);

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsJsonAsync(uri, args, cancelToken);

            Log(LogLevel.Information, args.JobId, args.TaskInfo.TaskId, args.TaskInfo.TaskRequeueCount,
                "Report task completion to {uri}. Response code: {code}", uri, response.StatusCode);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            LogError(ex, args.JobId, args.TaskInfo.TaskId, args.TaskInfo.TaskRequeueCount,
                "Failed when reporting task completion to {uri}", uri);

            ResyncAndInvalidateCache();
        }
    }

    private void ResyncAndInvalidateCache()
    {
        _namingClient.InvalidateCache();
        _resyncFlag.RequestResync = true;
    }

    public Task<TaskInfo?> EndTaskAsync(EndTaskArgs args, string callbackUri)
    {
        Log(LogLevel.Information, args.JobId, args.TaskId, null, "EndTask: Started.");
        lock (_lock)
        {
            var taskInfo = _jobTaskTable.GetTask(args.JobId, args.TaskId);
            if (taskInfo == null)
            {
                Log(LogLevel.Warning, args.JobId, args.TaskId, null, "EndTask: Task is already finished.");
                return Task.FromResult<TaskInfo?>(null);
            }

            Log(LogLevel.Debug, taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount,
                "EndTask for ProcessKey {key}, processes count {count}", taskInfo.ProcessKey, _processes.Count);

            var stat = TerminateTask(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount, taskInfo.ProcessKey,
                (int)ErrorCodes.EndTaskExitCode, args.TaskCancelGracePeriodSeconds == 0, !taskInfo.PrimaryTask);

            taskInfo.ExitCode = (int)ErrorCodes.EndTaskExitCode;

            if (stat == null || stat.IsTerminated)
            {
                _jobTaskTable.RemoveTask(taskInfo.JobId, taskInfo.TaskId, taskInfo.AttemptId);

                taskInfo.Exited = true;
                taskInfo.CancelGracefulPeriod?.Cancel();

                if (stat != null)
                {
                    taskInfo.AssignFromStat(stat);
                }
            }
            else
            {
                taskInfo.Exited = false;
                taskInfo.AssignFromStat(stat);

                if (taskInfo.CancelGracefulPeriod != null && !taskInfo.CancelGracefulPeriod.IsCancellationRequested)
                {
                    taskInfo.CancelGracefulPeriod.Cancel();
                }
                taskInfo.CancelGracefulPeriod = new CancellationTokenSource();

                //Let the following lambda capture the copy instead of the original taskInfo.
                var taskInfoCopy = taskInfo.Copy();

                Task.Delay(args.TaskCancelGracePeriodSeconds * 1000, taskInfo.CancelGracefulPeriod.Token)
                    .ContinueWith(_ =>
                    {
                        TerminateTaskAfterGracefulPeriod(taskInfoCopy.JobId, taskInfoCopy.TaskId, taskInfoCopy.TaskRequeueCount,
                            taskInfoCopy.ProcessKey, callbackUri);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);
            }

            Log(LogLevel.Information, taskInfo.JobId, taskInfo.TaskId, null, "EndTask: Ended with result: {task}", taskInfo);

            //TODO: Avoid copy/"copy twice"?
            return Task.FromResult<TaskInfo?>(taskInfo.Copy());
        }
    }

    //NOTE: The caller must have lock to _lock object already.
    private ProcessStatistics? TerminateTask(int jobId, int taskId, int requeueCount, ulong processKey, 
        int exitCode, bool forced, bool mpiDockerTask)
    {
        if (mpiDockerTask)
        {
            throw new NotImplementedException();
        }
        else
        {
            _processes.TryGetValue(processKey, out var process);

            if (process == null)
            {
                Log(LogLevel.Warning, jobId, taskId, requeueCount, "No process is found for the task.");
                return null;
            }

            Log(LogLevel.Debug, jobId, taskId, requeueCount, "Try to kill the process. Forced: {forced}", forced);
            process.Kill(exitCode, forced);

            var stat = process.GetStatisticsFromCGroup();
            var times = 10;

            while (!stat.IsTerminated && times-- > 0)
            {
                Task.Delay(100).Wait();
                stat = process.GetStatisticsFromCGroup();
            }

            if (!stat.IsTerminated)
            {
                Log(LogLevel.Warning, jobId, taskId, requeueCount, 
                    "The task didn't exit within 1s. Process Ids: {ids}", string.Join(' ', stat.ProcessIds));
            }
            return stat;
        }
    }

    private void TerminateTaskAfterGracefulPeriod(int jobId, int taskId, int requeueCount, ulong processKey, string callbackUri)
    {
        Log(LogLevel.Information, jobId, taskId, null, "TerminateTaskAfterGracefulPeriod: Started.");

        lock (_lock)
        {
            var taskInfo = _jobTaskTable.GetTask(jobId, taskId);
            if (taskInfo == null)
            {
                Log(LogLevel.Warning, jobId, taskId, null, "TerminateTaskAfterGracefulPeriod: Task is already finished.");
            }
            else
            {
                var stat = TerminateTask(jobId, taskId, requeueCount, processKey, (int)ErrorCodes.EndTaskExitCode, true, false);
                if (stat != null)
                {
                    taskInfo.Exited = true;
                    taskInfo.ExitCode = (int)ErrorCodes.EndTaskExitCode;
                    taskInfo.AssignFromStat(stat);
                    taskInfo.ProcessIds?.Clear();

                    _jobTaskTable.RemoveTask(taskInfo.JobId, taskInfo.TaskId, taskInfo.AttemptId);

                    Log(LogLevel.Information, jobId, taskId, null, "TerminateTaskAfterGracefulPeriod: Ended with result: {task}", taskInfo);

                    //TODO: Move this out of the lock?
                    ReportTaskCompletionAsync(taskInfo.ToTaskCompletionEventArgs(), callbackUri).Wait();
                }
                else
                {
                    //Log?
                }
            }
        }
    }

    public Task<JobInfo?> EndJobAsync(EndJobArgs args)
    {
        Log(LogLevel.Information, args.JobId, null, null, "EndJob: Started.");

        lock (_lock)
        {
            var jobInfo = _jobTaskTable.RemoveJob(args.JobId);
            if (jobInfo == null)
            {
                Log(LogLevel.Warning, args.JobId, null, null, "EndJob: Job is already finished.");
            }
            else if (jobInfo.Tasks != null)
            {
                foreach (var taskInfo in jobInfo.Tasks.Values)
                {
                    Log(LogLevel.Debug, taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount, "EnbJob: Terminating task.");
                    var stat = TerminateTask(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount, 
                        taskInfo.ProcessKey, (int)ErrorCodes.EndJobExitCode, true, !taskInfo.PrimaryTask);
                    if (stat != null)
                    {
                        taskInfo.Exited = stat.IsTerminated;
                        taskInfo.ExitCode = (int)ErrorCodes.EndJobExitCode;
                        taskInfo.AssignFromStat(stat);
                        taskInfo.CancelGracefulPeriod?.Cancel();
                    }
                }
            }

            if (_jobUsers.TryGetValue(args.JobId, out var jobUser))
            {
                var cleanupUser = false;
                var username = jobUser.Item1;

                Log(LogLevel.Information, args.JobId, null, null, "EndJob: Clean up user {user}.", username);

                if (!_userJobs.TryGetValue(username, out var jobs))
                {
                    cleanupUser = true;
                }
                else
                {
                    jobs.Remove(args.JobId);
                    var jobCount = jobs.Count();

                    Log(LogLevel.Information, args.JobId, null, null, "EndJob: {0} jobs associated with the user {1}", jobCount, username);

                    if (jobCount == 0)
                    {
                        cleanupUser = true;
                        _userJobs.Remove(username);
                    }
                }

                _jobUsers.Remove(args.JobId);

                if (cleanupUser)
                {
                    CleanupUserAccount(jobUser);
                }
            }

            return Task.FromResult<JobInfo?>(jobInfo);
        }
    }

    public Task<string> PeekTaskOutputAsync(PeekTaskOutputArgs args)
    {
        throw new NotImplementedException();
    }

    public int GetJobCount()
    {
        lock (_lock)
        {
            return _jobTaskTable.GetJobCount();
        }
    }

    public int GetTaskCount()
    {
        lock (_lock)
        {
            return _jobTaskTable.GetTaskCount();
        }
    }

    public int GetCoresInUse()
    {
        lock (_lock)
        {
            return _jobTaskTable.GetCoresInUse();
        }
    }

    public IEnumerable<JobInfo> GetJobs()
    {
        lock (_lock)
        {
            UpdateTaskStatistics();
            return _jobTaskTable.GetJobs(true);
        }
    }

    private void UpdateTaskStatistics()
    {
        lock (_lock)
        {
            foreach (var taskInfo in _jobTaskTable.GetAllTasks())
            {
                if (_processes.TryGetValue(taskInfo.ProcessKey, out var process))
                {
                    taskInfo.AssignFromStat(process.GetStatisticsFromCGroup());
                }
                else
                {
                    Log(LogLevel.Warning, taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount,
                        "No process object is found when updating task statistics.");
                }
            }
        }
    }
}
