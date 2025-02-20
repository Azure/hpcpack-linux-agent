using Microsoft.Extensions.Logging;
using NodeAgent.Models;
using NodeAgent.Utils;
using System.Diagnostics;

namespace NodeAgent.Services;

//All methods of the interface are thread-safe.
public interface IJobTaskExecutor
{
    Task StartJobAndTaskAsync(StartJobAndTaskArgs args, string callbackUri, CancellationToken cancellationToken = default);

    Task StartTaskAsync(StartTaskArgs args, string callbackUri, CancellationToken cancellationToken = default);

    Task<IReadOnlyTaskInfo?> EndTaskAsync(EndTaskArgs args, string callbackUri, CancellationToken cancellationToken = default);

    Task<IReadOnlyJobInfo?> EndJobAsync(EndJobArgs args, CancellationToken cancellationToken = default);

    Task<string?> PeekTaskOutputAsync(PeekTaskOutputArgs args, CancellationToken cancellationToken = default);

    int GetJobCount();

    int GetTaskCount();

    int GetCoresInUse();

    IEnumerable<IReadOnlyJobInfo> GetJobs();
}

/*
 * TODO
 *
 * Review locks in JobTaskExecutor and JobTaskTable for thread-safety, deadlock and performance.
 */
public class JobTaskExecutor : IJobTaskExecutor
{
    private class TaskProcessNotFound : ApplicationException
    {
        public TaskProcessNotFound(string msg) : base(msg) { }
    }

    private ILogger _logger;
    private ISchedulerApiClient _schedulerApiClient;
    private IResyncFlag _resyncFlag;
    private ISystemService _systemService;
    private ITaskProcessFactory _processFactory;

    private JobTaskTable _jobTaskTable = new JobTaskTable();
    private IDictionary<int, UserInfo> _jobUsers = new Dictionary<int, UserInfo>();
    private IDictionary<string, ISet<int>> _userJobs = new Dictionary<string, ISet<int>>();
    private IDictionary<ulong, ITaskProcess> _processes = new Dictionary<ulong, ITaskProcess>();
    private object _lock = new object();

    public JobTaskExecutor(
        ILogger<JobTaskExecutor> logger,
        ISchedulerApiClient schedulerApiClient,
        IResyncFlag resyncFlag,
        ISystemService systemService,
        ITaskProcessFactory processFactory)
    {
        _logger = logger;
        _schedulerApiClient = schedulerApiClient;
        _resyncFlag = resyncFlag;
        _systemService = systemService;
        _processFactory = processFactory;
    }

    private string GetUserNameFromDomainUser(string domainUser)
    {
        var tokens = domainUser.Split('\\');
        return tokens.Length > 1 ? tokens[tokens.Length - 1] : domainUser;
    }

    private async Task<UserInfo> SetupUserAccountAsync(StartJobAndTaskArgs args, CancellationToken cancellationToken = default)
    {
        string? isAdminValue = null;
        string? mapAdminUserValue = null;
        args.StartInfo?.EnvironmentVariables?.TryGetValue("CCP_ISADMIN", out isAdminValue);
        args.StartInfo?.EnvironmentVariables?.TryGetValue("CCP_MAP_ADMIN_USER", out mapAdminUserValue);

        var isAdmin = string.Equals(isAdminValue, "1");
        var mapAdminUser = string.Equals(mapAdminUserValue, "1");
        var mapAdminToRoot = isAdmin && !mapAdminUser;
        var mapAdminToUser = isAdmin && mapAdminUser;

        var WindowsSystemUser = "NT AUTHORITY\\SYSTEM";
        var isWindowsSystemAccount = string.Equals(args.UserName, WindowsSystemUser, StringComparison.OrdinalIgnoreCase);

        string? userName = null;
        bool existed = false;

        // Use root user in 3 scenarios:
        // 1. This is old image, username is empty, we use root.
        // 2. User is Windows or HPC Administrator and CCP_MAP_ADMIN_USER is not set
        // 3. User is Windows local system account, which is mapped to Linux root user.
        if (string.IsNullOrEmpty(args.UserName) || mapAdminToRoot || isWindowsSystemAccount)
        {
            userName = "root";
            existed = true;
        }
        else
        {
            string? preserveDomainValue = null;
            args.StartInfo?.EnvironmentVariables?.TryGetValue("CCP_PRESERVE_DOMAIN", out preserveDomainValue);

            var preserveDomain = string.Equals(preserveDomainValue, "1");
            userName = preserveDomain ? args.UserName : GetUserNameFromDomainUser(args.UserName);
            if (string.Equals(userName, "root"))
            {
                userName = "hpc_faked_root";
            }

            existed = !(await _systemService.CreateUserAsync(userName, args.Password, isAdmin, cancellationToken).ConfigureAwait(false));

            _logger.LogDebug(args.JobId, args.TaskId, null,
                "User '{user}' is {op} on node.", userName, existed ? "found" : "created");
        }

        bool privateKeyAdded = false;
        bool publicKeyAdded = false;
        bool authKeyAdded = false;

        // Set SSH keys in 3 scenarios:
        // 1. User is not a Windows or HPC Administrator.
        // 2. User is Windows or HPC Administrator and it is mapped to non-root user in Linux.
        // 3. User is Windows local system account, which is mapped to Linux root user.
        if (!string.IsNullOrEmpty(args.PrivateKey) && (!isAdmin || mapAdminToUser || isWindowsSystemAccount))
        {
            //TODO/refactor: Consider a single shell script for all the SSH key operations for better performance.
            try
            {
                var privateKeyFile = await _systemService.AddSshKeyAsync(userName, args.PrivateKey, true, cancellationToken).ConfigureAwait(false);
                privateKeyAdded = true;

                if (string.IsNullOrEmpty(args.PublicKey))
                {
                    args.PublicKey = await _systemService.GenerateSshPublicKeyAsync(privateKeyFile, cancellationToken).ConfigureAwait(false);
                }

                await _systemService.AddSshKeyAsync(userName, args.PublicKey, false, cancellationToken).ConfigureAwait(false);
                publicKeyAdded = true;

                await _systemService.AddAuthorizedKeyAsync(userName, args.PublicKey, cancellationToken).ConfigureAwait(false);
                authKeyAdded = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, args.JobId, args.TaskId, null, "Error when adding SSH key for user {user}.", userName);
            }

            _logger.LogDebug(args.JobId, args.TaskId, null,
                "Add SSH key for user {user} result: private {private}, public {public}, auth {auth}",
                userName, privateKeyAdded, publicKeyAdded, authKeyAdded);
        }
        else
        {
            _logger.LogDebug(args.JobId, args.TaskId, null, "Do not add SSH key for user {user}", userName);
        }

        return new UserInfo(userName, existed, privateKeyAdded , publicKeyAdded, authKeyAdded, args.PublicKey);
    }

    private async Task CleanupUserAccountAsync(UserInfo userInfo, int jobId, CancellationToken cancellationToken = default)
    {
        var (userName, existed, privateKeyAdded, publicKeyAdded, authKeyAdded, publicKey) = userInfo;

        _logger.LogDebug(jobId, null, null,
            "Remove SSH key for user {user}: private {private}, public {public}, auth {auth}",
            userName, privateKeyAdded, publicKeyAdded, authKeyAdded);

        if (privateKeyAdded)
        {
            try
            {
                await _systemService.RemoveSshKeyAsync(userName, true, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, jobId, null, null, "Error when removing SSH private key");
            }
        }

        if (publicKeyAdded)
        {
            try
            {
                await _systemService.RemoveSshKeyAsync(userName, false, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, jobId, null, null, "Error when removing SSH public key");
            }
        }

        if (authKeyAdded)
        {
            try
            {
                await _systemService.RemoveAuthorizedKeyAsync(userName, publicKey!, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, jobId, null, null, "Error when removing SSH authorized key");
            }
        }
    }

    public async Task StartJobAndTaskAsync(StartJobAndTaskArgs args, string callbackUri, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        lock (_lock)
        {
            var user = SetupUserAccountAsync(args, cancellationToken).Result;
            var userName = user.Item1;

            var added = _jobUsers.TryAdd(args.JobId, user);
            _logger.LogDebug(args.JobId, args.TaskId, null, "User '{user}' is added to jobUsers table.", userName);

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
        await StartTaskAsync(args.ToStartTaskArgs(), callbackUri, cancellationToken).ConfigureAwait(false);
    }

    public Task StartTaskAsync(StartTaskArgs args, string callbackUri, CancellationToken cancellationToken = default)
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
                _logger.LogInformation(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount,
                    "MPI non-master task found, skip creating the process.");

                throw new NotImplementedException();
            }
            else
            {
                if (!isNewEntry || _processes.ContainsKey(taskInfo.ProcessKey))
                {
                    _logger.LogWarning(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount, "The task has already started.");
                }
                else
                {
                    var process = _processFactory.CreateProcess(
                        taskInfo.JobId,
                        taskInfo.TaskId,
                        taskInfo.TaskRequeueCount,
                        "Task",
                        args.StartInfo.CommandLine,
                        args.StartInfo.StdOutFile,
                        args.StartInfo.StdErrFile,
                        args.StartInfo.StdInFile,
                        args.StartInfo.WorkDirectory,
                        userName,
                        true,
                        args.StartInfo.Affinity,
                        args.StartInfo.EnvironmentVariables,
                        (exitCode, message, stat) =>
                        {
                            OnTaskProcessComplete(taskInfo, callbackUri, exitCode, message, stat);
                        });

                    _processes[taskInfo.ProcessKey] = process;

                    _logger.LogDebug(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount,
                        "Start process with ProcessKey {key} and process count {count}", taskInfo.ProcessKey, _processes.Count);

                    return process.StartAsync();
                }
            }
            return Task.CompletedTask;
        }
    }

    private void OnTaskProcessComplete(TaskInfo taskInfo, string callbackUri, int processExitCode, string processMessage, ProcessStatistics? stat)
    {
        lock (_lock)
        {
            taskInfo.CancelGracefulPeriod?.Cancel();

            if (taskInfo.Exited)
            {
                _logger.LogDebug(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount, "Task has already been ended by EndTask.");
            }
            else
            {
                _logger.LogDebug(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount, "Task is complete.");

                taskInfo.Exited = true;
                taskInfo.ExitCode = processExitCode;
                taskInfo.Message = processMessage;
                taskInfo.AssignFromStat(stat);

                ReportTaskCompletionAsync(taskInfo, callbackUri).Wait();
            }

            //This won't remove the task entry added later as attempt id doesn't match
            _jobTaskTable.RemoveTask(taskInfo.JobId, taskInfo.TaskId, taskInfo.AttemptId);

            _logger.LogDebug(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount,
                "Remove task process: ProcessKey {key}, AttemptId {id}", taskInfo.ProcessKey, taskInfo.AttemptId);

            _processes.Remove(taskInfo.ProcessKey);
        }
    }

    private async Task ReportTaskCompletionAsync(TaskInfo taskInfo, string uri, CancellationToken cancelToken = default)
    {
        try
        {
            var args = new TaskCompletionEventArgs()
            {
                JobId = taskInfo.JobId,
                TaskInfo = taskInfo,
                NodeName = _systemService.HostName,
            };
            await _schedulerApiClient.ReportTaskCompletionAsync(uri, args, cancelToken).ConfigureAwait(false);

            _logger.LogInformation(args.JobId, args.TaskInfo.TaskId, args.TaskInfo.TaskRequeueCount,
                "Report task completion to {uri}. OK", uri);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount,
                "Error when reporting task completion to {uri}", uri);

            _resyncFlag.RequestResync = true;
        }
    }

    public Task<IReadOnlyTaskInfo?> EndTaskAsync(EndTaskArgs args, string callbackUri, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(args.JobId, args.TaskId, null, "EndTask: Started.");
        lock (_lock)
        {
            var taskInfo = _jobTaskTable.GetTask(args.JobId, args.TaskId);
            if (taskInfo == null)
            {
                _logger.LogWarning(args.JobId, args.TaskId, null, "EndTask: Task is already finished.");
                return Task.FromResult<IReadOnlyTaskInfo?>(null);
            }

            _logger.LogDebug(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount, "TaskInfo: {task}", taskInfo);
            _logger.LogDebug(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount,
                "EndTask for ProcessKey {key}, processes count {count}", taskInfo.ProcessKey, _processes.Count);

            try
            {
                var stat = TerminateTask(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount, taskInfo.ProcessKey,
                    (int)ErrorCodes.EndTaskExitCode, args.TaskCancelGracePeriodSeconds == 0, !taskInfo.PrimaryTask, cancellationToken);

                if (stat == null || stat.IsTerminated)
                {
                    _logger.LogDebug(args.JobId, args.TaskId, null, "EndTask: Task is terminated.");

                    taskInfo.Exited = true;
                    taskInfo.ExitCode = (int)ErrorCodes.EndTaskExitCode;
                    taskInfo.CancelGracefulPeriod?.Cancel();
                    taskInfo.AssignFromStat(stat);
                    ReportTaskCompletionAsync(taskInfo, callbackUri).Wait();
                    _jobTaskTable.RemoveTask(taskInfo.JobId, taskInfo.TaskId, taskInfo.AttemptId);
                }
                else
                {
                    _logger.LogDebug(args.JobId, args.TaskId, null, "EndTask: Task is not terminated. Try to terminate it after {time} seconds.",
                        args.TaskCancelGracePeriodSeconds);

                    taskInfo.Exited = false;
                    taskInfo.AssignFromStat(stat);
                    taskInfo.CancelGracefulPeriod?.Cancel();
                    taskInfo.CancelGracefulPeriod = new CancellationTokenSource();

                    //Let the following lambda capture this variable instead of the original taskInfo.
                    var capture = new
                    {
                        JobId = taskInfo.JobId,
                        TaskId = taskInfo.TaskId,
                        TaskRequeueCount = taskInfo.TaskRequeueCount,
                        ProcessKey = taskInfo.ProcessKey,
                    };

                    Task.Delay(args.TaskCancelGracePeriodSeconds * 1000, taskInfo.CancelGracefulPeriod.Token)
                        .ContinueWith(_ =>
                        {
                            TerminateTaskAfterGracefulPeriod(capture.JobId, capture.TaskId, capture.TaskRequeueCount,
                                capture.ProcessKey, callbackUri);
                        }, TaskContinuationOptions.OnlyOnRanToCompletion);
                }

                _logger.LogInformation(taskInfo.JobId, taskInfo.TaskId, null, "EndTask: Ended with result: {task}", taskInfo);
            }
            catch (TaskProcessNotFound ex)
            {
                _logger.LogWarning(ex, args.JobId, args.TaskId, null, "EndTask: No task is found for key {key}.", taskInfo.ProcessKey);
            }

            return Task.FromResult<IReadOnlyTaskInfo?>(taskInfo);
        }
    }

    /*
     * NOTE
     *
     * The original C++ version method TerminateTask can terminate plain task or docker task. That is
     * a bad design as that makes the return value ambiguous: you cannot tell if you succeeded terminating
     * a task or just that the task is not found or that the task is a docker task when it return null.
     * So here TaskProcessNotFound is raised when a task is not found. And for docker task, a new method
     * should be made for that. That is a TODO.
     */
    private ProcessStatistics? TerminateTask(int jobId, int taskId, int requeueCount, ulong processKey,
        int exitCode, bool forced, bool mpiDockerTask, CancellationToken cancellationToken = default)
    {
        Trace.Assert(Monitor.IsEntered(_lock));

        if (mpiDockerTask)
        {
            //TODO: Move the function into a new method like TerminateDockerTask. See above notes.
            throw new NotImplementedException();
        }
        else
        {
            _processes.TryGetValue(processKey, out var process);
            if (process == null)
            {
                throw new TaskProcessNotFound($"No process is found for the key {processKey}.");
            }

            _logger.LogDebug(jobId, taskId, requeueCount, "Try to kill the process. Forced: {forced}", forced);

            process.KillAsync(forcedExitCode: exitCode, forced: forced).Wait();

            var times = 10;
            var stat = process.GetStatisticsFromCGroupAsync(cancellationToken).Result;
            while (stat != null && !stat.IsTerminated && times-- > 0)
            {
                Task.Delay(100).Wait(cancellationToken);
                stat = process.GetStatisticsFromCGroupAsync(cancellationToken).Result;
            }

            if (stat != null && !stat.IsTerminated)
            {
                _logger.LogWarning(jobId, taskId, requeueCount,
                    "The task didn't exit within 1s. Process Ids: {ids}", string.Join(' ', stat.ProcessIds));
            }
            return stat;
        }
    }

    private void TerminateTaskAfterGracefulPeriod(int jobId, int taskId, int requeueCount, ulong processKey, string callbackUri)
    {
        //TODO: Why requeue count is not set in log?
        _logger.LogInformation(jobId, taskId, null, "TerminateTaskAfterGracefulPeriod: Started.");

        lock (_lock)
        {
            var taskInfo = _jobTaskTable.GetTask(jobId, taskId);
            if (taskInfo == null)
            {
                _logger.LogWarning(jobId, taskId, null, "TerminateTaskAfterGracefulPeriod: Task is already finished.");
            }
            else
            {
                try
                {
                    var stat = TerminateTask(jobId, taskId, requeueCount, processKey, (int)ErrorCodes.EndTaskExitCode, true, false);

                    taskInfo.Exited = true;
                    taskInfo.ExitCode = (int)ErrorCodes.EndTaskExitCode;
                    taskInfo.AssignFromStat(stat);
                    taskInfo.ProcessIds?.Clear();

                    ReportTaskCompletionAsync(taskInfo, callbackUri).Wait();

                    _jobTaskTable.RemoveTask(taskInfo.JobId, taskInfo.TaskId, taskInfo.AttemptId);
                    _logger.LogInformation(jobId, taskId, null, "TerminateTaskAfterGracefulPeriod: Ended with result: {task}", taskInfo);
                }
                catch (TaskProcessNotFound ex)
                {
                    _logger.LogWarning(ex, jobId, taskId, requeueCount, "TerminateTaskAfterGracefulPeriod: No task is found for key {key}.", processKey);
                }
            }
        }
    }

    public Task<IReadOnlyJobInfo?> EndJobAsync(EndJobArgs args, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(args.JobId, null, null, "EndJob: Started.");

        lock (_lock)
        {
            var jobInfo = _jobTaskTable.RemoveJob(args.JobId);
            if (jobInfo == null)
            {
                _logger.LogWarning(args.JobId, null, null, "EndJob: Job is already finished.");
            }
            else if (jobInfo.Tasks != null)
            {
                foreach (var taskInfo in jobInfo.Tasks.Values)
                {
                    _logger.LogDebug(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount, "EnbJob: Terminating task.");

                    try
                    {
                        var stat = TerminateTask(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount,
                            taskInfo.ProcessKey, (int)ErrorCodes.EndJobExitCode, true, !taskInfo.PrimaryTask, cancellationToken);

                        taskInfo.Exited = stat?.IsTerminated ?? true;
                        taskInfo.ExitCode = (int)ErrorCodes.EndJobExitCode;
                        taskInfo.AssignFromStat(stat);
                        taskInfo.CancelGracefulPeriod?.Cancel();

                        //NOTE: No ReportTaskCompletionAsync is called here for each task. So no end message will be sent to the Schedular.
                        //Maybe an issue. But let's keep it as it is.
                    }
                    catch (TaskProcessNotFound ex)
                    {
                        _logger.LogWarning(ex, taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount,
                            "EnbJob: No task is found for key {key}.", taskInfo.ProcessKey);
                    }
                }
            }

            if (_jobUsers.TryGetValue(args.JobId, out var jobUser))
            {
                var cleanupUser = false;
                var username = jobUser.Item1;

                _logger.LogInformation(args.JobId, null, null, "EndJob: Clean up user {user}.", username);

                if (!_userJobs.TryGetValue(username, out var jobs))
                {
                    cleanupUser = true;
                }
                else
                {
                    jobs.Remove(args.JobId);
                    var jobCount = jobs.Count();

                    _logger.LogInformation(args.JobId, null, null, "EndJob: {0} jobs associated with the user {1}", jobCount, username);

                    if (jobCount == 0)
                    {
                        cleanupUser = true;
                        _userJobs.Remove(username);
                    }
                }

                _jobUsers.Remove(args.JobId);

                if (cleanupUser)
                {
                    CleanupUserAccountAsync(jobUser, args.JobId, cancellationToken).Wait();
                }
            }

            return Task.FromResult<IReadOnlyJobInfo?>(jobInfo);
        }
    }

    public Task<string?> PeekTaskOutputAsync(PeekTaskOutputArgs args, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(args.JobId, args.TaskId, null, "PeekTaskOutput");
        lock (_lock)
        {
            var taskInfo = _jobTaskTable.GetTask(args.JobId, args.TaskId);
            if (taskInfo != null)
            {
                _logger.LogDebug(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount,
                    "PeekTaskOutput for ProcessKey {key}, process count {count}", taskInfo.ProcessKey, _processes.Count);

                if (_processes.TryGetValue(taskInfo.ProcessKey, out var process))
                {
                    return Task.FromResult(process.PeekOutputAsync(cancellationToken).Result);
                }
            }
        }
        return Task.FromResult<string?>(null);
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

    public IEnumerable<IReadOnlyJobInfo> GetJobs()
    {
        lock (_lock)
        {
            UpdateTaskStatistics();
            return _jobTaskTable.GetReadOnlyJobs();
        }
    }

    private void UpdateTaskStatistics()
    {
        lock (_lock)
        {
            foreach (var taskInfo in _jobTaskTable.GetTasks())
            {
                if (_processes.TryGetValue(taskInfo.ProcessKey, out var process))
                {
                    var stat = process.GetStatisticsFromCGroupAsync().Result;
                    taskInfo.AssignFromStat(stat);
                }
                else
                {
                    _logger.LogWarning(taskInfo.JobId, taskInfo.TaskId, taskInfo.TaskRequeueCount,
                        "No process object is found when updating task statistics.");
                }
            }
        }
    }
}
