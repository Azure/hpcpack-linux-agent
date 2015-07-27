using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Hpc.Monitoring;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator.Monitoring
{
    public class MonitoringConfig
    {
        public IEnumerable<MetricDefinition> MetricDefinitions { get; private set; }

        public bool UpdateWhenChanged(IEnumerable<MetricDefinition> metricDefinitions)
        {
            if (this.MetricDefinitions == null || !this.MetricDefinitions.SequenceEqual(
                metricDefinitions,
                GenericEqualityComparer<MetricDefinition>.CreateComparer((a, b) => a.ValueEquals(b), a => a.GetHashCode())))
            {
                this.MetricDefinitions = metricDefinitions;
                return true;
            }

            return false;
        }
    }
}
