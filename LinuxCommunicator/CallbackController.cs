using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Hpc.Activation;
using Microsoft.Hpc.Scheduler.Communicator;
using Microsoft.Hpc.Communicators.LinuxCommunicator.Monitoring;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator
{
    public class CallbackController : ApiController
    {
        [HttpPost]
        [Route("api/{nodename}/computenodereported")]
        public int ComputeNodeReported(string nodeName, [FromBody] ComputeClusterNodeInformation nodeInfo)
        {
            try
            {
                var arg = new ComputeNodeInfoEventArg(nodeInfo.Name, nodeInfo);
                 LinuxCommunicator.Instance.Tracer.TraceInfo("Linux ComputeNodeReported. NodeName {0}, JobCount {1}", arg.NodeName, arg.NodeInfo.Jobs.Count);
                int nextPing = LinuxCommunicator.Instance.SchedulerCallbacks.ComputeNodeReported(arg);

                return nextPing;
            }
            catch (Exception ex)
            {
                LinuxCommunicator.Instance.Tracer.TraceException(ex);
            }

            return 5000;
        }

        [HttpPost]
        [Route("api/{nodename}/taskcompleted")]
        public NextOperation TaskCompleted(string nodeName, [FromBody] ComputeNodeTaskCompletionEventArg taskInfo)
        {
            try
            {
                LinuxCommunicator.Instance.Tracer.TraceInfo("Linux TaskCompleted. NodeName {0}, TaskId {1} ExitCode {2} TaskMessage {3}", taskInfo.NodeName, taskInfo.TaskInfo.TaskId, taskInfo.TaskInfo.ExitCode, taskInfo.TaskInfo.Message);
                return LinuxCommunicator.Instance.SchedulerCallbacks.TaskCompleted(taskInfo);
            }
            catch (Exception ex)
            {
                LinuxCommunicator.Instance.Tracer.TraceException(ex);
                return NextOperation.CancelJob;
            }
        }

        [HttpPost]
        [Route("api/{nodename}/metricreported")]
        public int MetricReported(string nodeName, [FromBody] ComputeNodeMetricInformation metricInfo)
        {
            try
            {
                LinuxCommunicator.Instance.Tracer.TraceInfo("Linux MetricReported. NodeName {0}, Metric Time {1} ", metricInfo.Name, metricInfo.Time);
                LinuxCommunicator.Instance.OnNodeMetricReported(metricInfo);

                return -1;
            }
            catch (Exception ex)
            {
                LinuxCommunicator.Instance.Tracer.TraceException(ex);
            }

            return 5000;
        }
    }
}
