using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator.Monitoring
{
    public class MetricCountersConfig
    {
        public IEnumerable<MetricCounter> MetricCounters { get; set; }
    }
}
