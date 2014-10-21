using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Hpc.Activation;
using Microsoft.Hpc.Scheduler.Communicator;

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
                int nextPing = LinuxCommunicator.Instance.Listener.ComputeNodeReported(arg);

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
                LinuxCommunicator.Instance.Tracer.TraceInfo("Linux TaskCompleted. NodeName {0}, TaskMessage {1}", taskInfo.NodeName, taskInfo.TaskInfo.Message);
                return LinuxCommunicator.Instance.Listener.TaskCompleted(taskInfo);
            }
            catch (Exception ex)
            {
                LinuxCommunicator.Instance.Tracer.TraceException(ex);
                return NextOperation.CancelJob;
            }
        }
    }
}
