using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator.Monitoring
{
    /// <summary>
    /// List the performance counter and catalog names for the scheduler
    /// </summary>
    class PerformanceCounterNames
    {
        public const string Scheduler_JobsCategory_Name = "HPC Jobs";
        public const string Scheduler_TasksCategory_Name = "HPC Tasks";
        public const string Scheduler_NodesCategory_Name = "HPC Nodes";
        public const string Scheduler_ProcessorsCategory_Name = "HPC Cores";
        public const string Scheduler_SchedulerCategory_Name = "HPC Scheduler";
        public const string Scheduler_PoolsCategory_Name = "HPC Pools";

        public const string Scheduler_ClusterPerfCounter_NumberOfConfiguringJobs_Name = "Number of configuring jobs";
        public const string Scheduler_ClusterPerfCounter_NumberOfQueuedJobs_Name = "Number of queued jobs";
        public const string Scheduler_ClusterPerfCounter_NumberOfRunningJobs_Name = "Number of running jobs";
        public const string Scheduler_ClusterPerfCounter_NumberOfFinishedJobs_Name = "Number of finished jobs";
        public const string Scheduler_ClusterPerfCounter_NumberOfFailedJobs_Name = "Number of failed jobs";
        public const string Scheduler_ClusterPerfCounter_NumberOfCanceledJobs_Name = "Number of canceled jobs";
        public const string Scheduler_ClusterPerfCounter_TotalNumberOfJobs_Name = "Total number of jobs";

        public const string Scheduler_ClusterPerfCounter_NumberOfConfiguringTasks_Name = "Number of configuring tasks";
        public const string Scheduler_ClusterPerfCounter_NumberOfQueuedTasks_Name = "Number of queued tasks";
        public const string Scheduler_ClusterPerfCounter_NumberOfRunningTasks_Name = "Number of running tasks";
        public const string Scheduler_ClusterPerfCounter_NumberOfFinishedTasks_Name = "Number of finished tasks";
        public const string Scheduler_ClusterPerfCounter_NumberOfFailedTasks_Name = "Number of failed tasks";
        public const string Scheduler_ClusterPerfCounter_NumberOfCanceledTasks_Name = "Number of canceled tasks";
        public const string Scheduler_ClusterPerfCounter_TotalNumberOfTasks_Name = "Total Number of tasks";

        public const string Scheduler_ClusterPerfCounter_NumberOfDrainingNodes_Name = "Number of draining nodes";
        public const string Scheduler_ClusterPerfCounter_NumberOfOfflineNodes_Name = "Number of offline nodes";
        public const string Scheduler_ClusterPerfCounter_NumberOfReadyNodes_Name = "Number of online nodes";
        public const string Scheduler_ClusterPerfCounter_TotalNumberOfNodes_Name = "Total number of nodes";
        public const string Scheduler_ClusterPerfCounter_NumberOfUnreachableNodes_Name = "Number of unreachable nodes";

        public const string Scheduler_ClusterPerfCounter_NumberOfOnlineProcessors_Name = "Number of online cores";
        public const string Scheduler_ClusterPerfCounter_NumberOfOfflineProcessors_Name = "Number of offline cores";
        public const string Scheduler_ClusterPerfCounter_NumberOfIdleProcessors_Name = "Number of idle cores";
        public const string Scheduler_ClusterPerfCounter_NumberOfBusyProcessors_Name = "Number of busy cores";
        public const string Scheduler_ClusterPerfCounter_NumberOUnreachablefProcessors_Name = "Number of unreachable cores";
        public const string Scheduler_ClusterPerfCounter_TotalNumberOfProcessors_Name = "Total number of cores";

        // Need concat with the resource pool name, see fix for bug 28879
        public const string Scheduler_ClusterPerfCounter_PoolGaurantee_Name = "Pool Guarantee";
        public const string Scheduler_ClusterPerfCounter_PoolCurrentAllocation_Name = "Pool Current Allocation";
    }
}
