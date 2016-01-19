using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Microsoft.Hpc.Activation;
using Microsoft.Hpc.Scheduler.Communicator;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator
{
    public class CallbackController : ApiController
    {
        /// <summary>
        /// The Http header UpdateId
        /// </summary>
        public const string UpdateIdHeaderName = "UpdateId";

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
        [Route("api/{nodename}/registerrequested")]
        public int RegisterRequested(string nodeName, [FromBody] RegisterEventArgs registerInfo)
        {
            try
            {
                if (!nodeName.Equals(registerInfo.NodeName, StringComparison.OrdinalIgnoreCase))
                {
                    LinuxCommunicator.Instance.Tracer.TraceError("Linux RegisterRequested: the node name '{0}' in register info mismatches the expected node name {1}.", nodeName, registerInfo.NodeName);
                    LinuxCommunicator.Instance.Tracer.TraceInfo("The IP address(es) of the node {0}: {1}.", registerInfo.NodeName, string.Join(",", registerInfo.NetworksInfo.Select(ni => ni.IpV4)));
                }
                else
                {
                    LinuxCommunicator.Instance.Tracer.TraceInfo("Linux RegisterRequested. NodeName {0}, Distro {1} ", registerInfo.NodeName, registerInfo.DistroInfo);
                    LinuxCommunicator.Instance.OnRegisterRequested(registerInfo);
                }

                return -1;
            }
            catch (Exception ex)
            {
                LinuxCommunicator.Instance.Tracer.TraceException(ex);
            }

            return 5000;
        }

        [HttpPost]
        [Route("api/{nodename}/getinstanceids")]
        public int[] GetInstanceIds(string nodeName, [FromBody] string[] instanceNames)
        {
            try
            {
                LinuxCommunicator.Instance.Tracer.TraceInfo("Linux GetInstanceIds. NodeName {0}, instanceNames {1}, {2}", nodeName, instanceNames.Length, string.Join(",", instanceNames));

                return LinuxCommunicator.Instance.MonitoringConfigManager.Store.GetMetricInstanceIds(instanceNames);
            }
            catch (Exception ex)
            {
                LinuxCommunicator.Instance.Tracer.TraceException(ex);
            }

            return null;
        }

        [HttpGet]
        [Route("api/hostsfile")]
        public HttpResponseMessage GetHosts()
        {
            Guid curUpdateId = LinuxCommunicator.Instance.HostsManager.UpdateId;
            IEnumerable<string> updateIds;
            bool hasUpdateId = Request.Headers.TryGetValues(UpdateIdHeaderName, out updateIds);
            HttpResponseMessage response = null;
            Guid guid;
            if (hasUpdateId && Guid.TryParse(updateIds.FirstOrDefault(), out guid) && guid == curUpdateId)
            {
                response = Request.CreateResponse(HttpStatusCode.NoContent);
            }
            else
            {
                response = Request.CreateResponse(HttpStatusCode.OK, LinuxCommunicator.Instance.HostsManager.ManagedEntries);
            }

            response.Headers.Add(UpdateIdHeaderName, curUpdateId.ToString());
            return response;
        }
    }
}
