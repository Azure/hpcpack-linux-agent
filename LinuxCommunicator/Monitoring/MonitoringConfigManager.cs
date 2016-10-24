using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Hpc.Monitoring;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator.Monitoring
{
    public sealed class MonitoringConfigManager : IDisposable
    {
        private MonitoringConfig currentConfig = new MonitoringConfig();

        private System.Timers.Timer checkConfigTimer = new System.Timers.Timer();

        private Dictionary<string, string[]> schedulerInstanceMap = new Dictionary<string, string[]>(StringComparer.CurrentCultureIgnoreCase)
        {
            { "HPCSchedulerJobs", 
                new string[]
                {
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOfCanceledJobs_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOfConfiguringJobs_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOfFailedJobs_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOfFinishedJobs_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOfQueuedJobs_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOfRunningJobs_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_TotalNumberOfJobs_Name
                }
            },

            { "HPCSchedulerNodes", 
                new string[]
                {
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOfDrainingNodes_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOfOfflineNodes_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOfReadyNodes_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOfUnreachableNodes_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_TotalNumberOfNodes_Name
                }
            },

            { "HPCSchedulerCores", 
                new string[]
                {
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOfOnlineProcessors_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOfOfflineProcessors_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOfIdleProcessors_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOfBusyProcessors_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_NumberOUnreachablefProcessors_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_TotalNumberOfProcessors_Name
                }
            },

            { "HPCPoolUsage", 
                new string[]
                {
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_PoolGaurantee_Name,
                    PerformanceCounterNames.Scheduler_ClusterPerfCounter_PoolCurrentAllocation_Name
                }
            },
        };

        private MetricCountersConfig metricCountersConfig = new MetricCountersConfig();

        public MonitoringConfigManager() { }

        public void Initialize(string server)
        {
            RetryManager rm = new RetryManager(new PeriodicRetryTimer(30 * 1000));
            while (true)
            {
                try
                {
                    this.Store = MonitoringStoreConnection.Connect(server, "LinuxCommunicator");
                    this.checkConfigTimer_Elapsed(this, null);
                    this.checkConfigTimer.AutoReset = true;
                    this.checkConfigTimer.Interval = 5 * 60 * 1000;
                    this.checkConfigTimer.Elapsed += checkConfigTimer_Elapsed;
                    break;
                }
                catch (Exception e)
                {
                    if (rm.HasAttemptsLeft)
                    {
                        LinuxCommunicator.Instance.Tracer.TraceException(e, "MonitoringConfigManager initialization failed. Retry count {0}, retry wait time {1}.", rm.RetryCount, rm.NextWaitTime);
                        rm.WaitForNextAttempt();
                    }
                }
            }
        }

        public event EventHandler<ConfigChangedEventArgs> ConfigChanged;

        public IHpcMonitoringStore Store { get; private set; }

        public MetricCountersConfig MetricCountersConfig { get { return this.metricCountersConfig; } }

        public void Start()
        {
            this.checkConfigTimer.Start();
        }

        public void Stop()
        {
            this.checkConfigTimer.Stop();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                this.checkConfigTimer.Dispose();
            }
        }

        private void checkConfigTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var metrics = this.Store.GetMetrics(MetricTarget.ComputeNode);
            if (this.currentConfig.UpdateWhenChanged(metrics))
            {
                Interlocked.Exchange(ref this.metricCountersConfig, new MetricCountersConfig() { MetricCounters = this.GetMetricCounters().ToList() });

                this.OnConfigChanged(this, new ConfigChangedEventArgs() { CurrentConfig = this.MetricCountersConfig });
            }
        }

        private string[] GetInstanceNames(MetricDefinition def)
        {
            if (!string.IsNullOrEmpty(def.InstanceFilter))
            {
                return new string[] { def.InstanceFilter };
            }

            return null;
        }

        private IEnumerable<MetricCounter> GetMetricCounters()
        {
            foreach (var def in this.currentConfig.MetricDefinitions.Where(
                d => d.Type == MetricType.Performance && !this.schedulerInstanceMap.Keys.Contains(d.Alias, StringComparer.InvariantCultureIgnoreCase)))
            {
                var instanceNames = this.GetInstanceNames(def);
                if (instanceNames != null && instanceNames.Length > 0)
                {
                    int[] instanceIds = this.Store.GetMetricInstanceIds(instanceNames);
                    if (instanceIds.Length != instanceNames.Length)
                    {
                        LinuxCommunicator.Instance.Tracer.TraceDetail("InstanceIds.Length {0} not equal to InstanceNames.Length {1}", instanceIds.Length, instanceNames.Length);
                        yield break;
                    }

                    for (int i = 0; i < instanceIds.Length; i++)
                    {
                        yield return new MetricCounter()
                        {
                            Path = string.Format(@"\{0}\{1}", def.Category, def.Name),
                            InstanceId = instanceIds[i],
                            MetricId = def.MetricId,
                            InstanceName = instanceNames[i],
                        };
                    }
                }
                else
                {
                    yield return new MetricCounter()
                    {
                        Path = string.Format(@"\{0}\{1}", def.Category, def.Name),
                        InstanceId = 0,
                        MetricId = def.MetricId,
                    };
                }
            }
        }

        private void OnConfigChanged(object sender, ConfigChangedEventArgs args)
        {
            var configChanged = this.ConfigChanged;
            if (configChanged != null)
            {
                configChanged(this, args);
            }
        }
    }
}
