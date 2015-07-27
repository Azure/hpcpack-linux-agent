using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator.Monitoring
{
    class ConfigChangedEventArgs : EventArgs
    {
        public MetricCountersConfig CurrentConfig { get; set; }
    }
}
