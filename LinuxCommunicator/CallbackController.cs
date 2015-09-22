using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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
                LinuxCommunicator.Instance.Tracer.TraceInfo("Linux RegisterRequested. NodeName {0}, Distro {1} ", registerInfo.NodeName, registerInfo.DistroInfo);
                LinuxCommunicator.Instance.OnRegisterRequested(registerInfo);

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
        [Route("api/hostfile")]
        public HttpResponseMessage GetHosts()
        {
            Guid curUpdateId = LinuxCommunicator.Instance.HostsManager.UpdateId;
            string updateid = string.Empty;
            IEnumerable<string> updateids;
            if (Request.Headers.TryGetValues("UpdateId", out updateids))
            {
                foreach (var id in updateids)
                {
                    updateid = id;
                }
            }

            HttpResponseMessage response = null;
            Guid guid;
            if (Guid.TryParse(updateid, out guid) && guid == curUpdateId)
            {
                response = Request.CreateResponse(HttpStatusCode.NoContent);
            }
            else
            {
                response = Request.CreateResponse(HttpStatusCode.OK, LinuxCommunicator.Instance.HostsManager.ManagedEntries);
            }

            response.Headers.Add("UpdateId", curUpdateId.ToString());
            return response;
        }
    }
}
