using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator.Monitoring
{
    public class MetricCounter
    {
        public string Path { get; set; }

        public int MetricId { get; set; }

        public int InstanceId { get; set; }

        public string InstanceName { get; set; }
    }
}
