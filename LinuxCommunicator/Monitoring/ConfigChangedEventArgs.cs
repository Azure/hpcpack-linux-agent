using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator.Monitoring
{
    public class ConfigChangedEventArgs : EventArgs
    {
        public MetricCountersConfig CurrentConfig { get; set; }
    }
}
