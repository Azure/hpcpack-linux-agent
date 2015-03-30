using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Hpc.Activation;
using Microsoft.Hpc.Scheduler;
using Microsoft.Hpc.Scheduler.Communicator;
using System.Net;
using Microsoft.Hpc.Scheduler.Properties;
using Microsoft.Hpc.Communicators.LinuxCommunicator.Monitoring;
using System.IO;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator
{
    public class LinuxCommunicator : IManagedResourceCommunicator, IDisposable
    {
        private const string ResourceUriFormat = "http://{0}:50000/api/{1}/{2}";
        private const string CallbackUriHeaderName = "CallbackUri";
        private const string HpcFullKeyName = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\HPC";
        private const string ClusterNameKeyName = "ClusterName";

        private readonly string HeadNode = (string)Microsoft.Win32.Registry.GetValue(HpcFullKeyName, ClusterNameKeyName, null);
        private readonly int MonitoringPort = 9894;

        private HttpClient client;
        private WebServer server;
        private Monitoring.CounterDataSender sender;
        private Dictionary<string, Guid> nodeMap;

        private CancellationTokenSource cancellationTokenSource;

        private static LinuxCommunicator instance;

        public LinuxCommunicator()
        {
            if (instance != null)
            {
                throw new InvalidOperationException("An instance of LinuxCommunicator already exists.");
            }

            instance = this;
        }

        public void Dispose()
        {
            this.sender.CloseConnection();
            this.server.Dispose();
            this.client.Dispose();
            GC.SuppressFinalize(this);
        }

        public static LinuxCommunicator Instance
        {
            get { return instance; }
        }

        public string LinuxServiceHost
        {
            get;
            set;
        }

        public INodeCommTracing Tracer
        {
            get;
            private set;
        }

        public ISchedulerCallbacks SchedulerCallbacks
        {
            get;
            set;
        }

        public NodeLocation Location { get { return NodeLocation.Linux; } }

        public event EventHandler<NodeMetricReportedEventArgs> NodeMetricReported;

        public void SetTracer(INodeCommTracing tracer)
        {
            this.Tracer = tracer;
        }

        public bool Initialize()
        {
            this.Tracer.TraceInfo("Initializing LinuxCommunicator.");

            ServicePointManager.ServerCertificateValidationCallback += (s, cert, chain, sslPolicyErrors) =>
            {
                if (chain != null && chain.ChainStatus.Length == 1 && chain.ChainStatus[0].Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot)
                {
                    return true;
                }

                return sslPolicyErrors == System.Net.Security.SslPolicyErrors.None;
            };

            this.client = new HttpClient();
            this.server = new WebServer();

            if (this.HeadNode == null)
            {
                ArgumentNullException exp = new ArgumentNullException(ClusterNameKeyName);
                Tracer.TraceError("Failed to find registry value: {0}. {1}", ClusterNameKeyName, exp);
                throw exp;
            }

            this.sender = new Monitoring.CounterDataSender();
            this.sender.OpenConnection(this.HeadNode, this.MonitoringPort);

            using (IScheduler scheduler = new Scheduler.Scheduler())
            {
                scheduler.Connect(this.HeadNode);
                this.nodeMap = new Dictionary<string, Guid>();
                FilterCollection fc = new FilterCollection();
                fc.Add(FilterOperator.Equal, NodePropertyIds.Location, NodeLocation.Linux);
                foreach (ISchedulerNode node in scheduler.GetNodeList(fc, null))
                {
                    this.nodeMap.Add(node.Name.ToLower(), node.Guid);
                }

                scheduler.Close();
            }

            this.Tracer.TraceInfo("Initialized LinuxCommunicator.");
            return true;
        }

        public bool Start()
        {
            this.Tracer.TraceInfo("Starting LinuxCommunicator.");
            this.cancellationTokenSource = new CancellationTokenSource();
            this.server.Start().Wait();

            return true;
        }

        public bool Stop()
        {
            this.Tracer.TraceInfo("Stopping LinuxCommunicator.");
            this.server.Stop();
            this.cancellationTokenSource.Cancel();
            this.cancellationTokenSource.Dispose();
            this.cancellationTokenSource = null;
            return true;
        }

        public void OnNodeStatusChange(string nodeName, bool reachable)
        {
        }

        public void EndJob(string nodeName, EndJobArg arg, NodeCommunicatorCallBack<EndJobArg> callback)
        {
            this.SendRequest("endjob", "taskcompleted", nodeName, async (content, ex) =>
            {
                Exception readEx = null;

                try
                {
                    arg.JobInfo = await content.ReadAsAsync<ComputeClusterJobInformation>();
                }
                catch (Exception e)
                {
                    this.Tracer.TraceError("Exception while read the task info {0}", e);

                    readEx = e;
                }

                if (ex != null && readEx != null)
                {
                    ex = new AggregateException(ex, readEx);
                }
                else
                {
                    ex = ex ?? readEx;
                }

                callback(nodeName, arg, ex);
            }, arg);
        }

        public void EndTask(string nodeName, EndTaskArg arg, NodeCommunicatorCallBack<EndTaskArg> callback)
        {
            this.SendRequest("endtask", "taskcompleted", nodeName, async (content, ex) =>
            {
                Exception readEx = null;

                try
                {
                    arg.TaskInfo = await content.ReadAsAsync<ComputeClusterTaskInformation>();
                }
                catch(Exception e)
                {
                    this.Tracer.TraceError("Exception while read the task info {0}", e);

                    readEx = e;
                }

                if (ex != null && readEx != null)
                {
                    ex = new AggregateException(ex, readEx);
                }
                else
                {
                    ex = ex ?? readEx;
                }

                callback(nodeName, arg, ex);
            }, arg);
        }

        public void StartJobAndTask(string nodeName, StartJobAndTaskArg arg, string userName, string password, ProcessStartInfo startInfo, NodeCommunicatorCallBack<StartJobAndTaskArg> callback)
        {
            this.SendRequest("startjobandtask", "taskcompleted", nodeName, (content, ex) =>
            {
                callback(nodeName, arg, ex);
            }, Tuple.Create(arg, startInfo));
        }

        public void StartJobAndTaskSoftCardCred(string nodeName, StartJobAndTaskArg arg, string userName, string password, byte[] certificate, ProcessStartInfo startInfo, NodeCommunicatorCallBack<StartJobAndTaskArg> callback)
        {
            this.SendRequest("startjobandtask", "taskcompleted", nodeName, (content, ex) =>
            {
                callback(nodeName, arg, ex);
            }, Tuple.Create(arg, startInfo));
        }

        public void StartTask(string nodeName, StartTaskArg arg, ProcessStartInfo startInfo, NodeCommunicatorCallBack<StartTaskArg> callback)
        {
            this.SendRequest("starttask", "taskcompleted", nodeName, (content, ex) =>
            {
                callback(nodeName, arg, ex);
            }, Tuple.Create(arg, startInfo));
        }

        public void Ping(string nodeName)
        {
            this.SendRequest<NodeCommunicatorCallBackArg>("ping", "computenodereported", nodeName, (content, ex) =>
            {
                this.Tracer.TraceInfo("Compute node {0} pinged. Ex {1}", nodeName, ex);
            }, null);

            this.Metric(nodeName);
        }

        public void Metric(string nodeName)
        {
            this.SendRequest<NodeCommunicatorCallBackArg>("metric", "metricreported", nodeName, (content, ex) =>
            {
                this.Tracer.TraceInfo("Compute node {0} metric requested. Ex {1}", nodeName, ex);
            }, null);
        }

        private void SendRequest<T>(string action, string callbackAction, string nodeName, Action<HttpContent, Exception> callback, T arg) where T : NodeCommunicatorCallBackArg
        {
            var request = new HttpRequestMessage(HttpMethod.Post, this.GetResoureUri(nodeName, action));
            request.Headers.Add(CallbackUriHeaderName, this.GetCallbackUri(nodeName, callbackAction));
            var formatter = new JsonMediaTypeFormatter();
            request.Content = new ObjectContent<T>(arg, formatter);

            this.client.SendAsync(request, this.cancellationTokenSource.Token).ContinueWith(t =>
            {
                Exception ex = t.Exception;

                if (ex == null)
                {
                    try
                    {
                        t.Result.EnsureSuccessStatusCode();
                    }
                    catch (Exception e)
                    {
                        ex = e;
                    }
                }

                callback(t.Result.Content, ex);
            }, this.cancellationTokenSource.Token);
        }

        private void SendRequest<T, T1>(string action, string callbackAction, string nodeName, Action<HttpContent, Exception> callback, Tuple<T, T1> arg) where T : NodeCommunicatorCallBackArg
        {
            var request = new HttpRequestMessage(HttpMethod.Post, this.GetResoureUri(nodeName, action));
            request.Headers.Add(CallbackUriHeaderName, this.GetCallbackUri(nodeName, callbackAction));
            var formatter = new JsonMediaTypeFormatter();
            request.Content = new ObjectContent<Tuple<T, T1>>(arg, formatter);

            this.client.SendAsync(request, this.cancellationTokenSource.Token).ContinueWith(t =>
            {
                Exception ex = t.Exception;

                if (ex == null)
                {
                    try
                    {
                        t.Result.EnsureSuccessStatusCode();
                    }
                    catch (Exception e)
                    {
                        ex = e;
                    }
                }

                callback(t.Result.Content, ex);
            }, this.cancellationTokenSource.Token);
        }

        private Uri GetResoureUri(string nodeName, string action)
        {
            var hostName = this.LinuxServiceHost ?? nodeName;
            return new Uri(string.Format(ResourceUriFormat, hostName, nodeName, action));
        }

        private string GetCallbackUri(string nodeName, string action)
        {
            return string.Format("{0}/api/{1}/{2}", this.server.ListeningUri, nodeName, action);
        }

        public void OnNodeMetricReported(Monitoring.ComputeNodeMetricInformation metricInfo)
        {
            var nodeMetricReported = this.NodeMetricReported;
            if (nodeMetricReported != null)
            {
                //this.Tracer.TraceDetail("Metric Name {0}, cores {1}, sockets {2}, memory {3}, ipaddress {4}", metricInfo.Name, metricInfo.CoreCount, metricInfo.SocketCount, metricInfo.MemoryMegabytes, metricInfo.IpAddress);

                //if (metricInfo.NetworkInfo != null)
                //{
                //    foreach (var net in metricInfo.NetworkInfo)
                //    {
                //        this.Tracer.TraceDetail("Network {0}, {1}, {2}, {3}, {4}, {5}", net.Name, net.MacAddress, net.IpV4, net.IpV6, net.IsIB, metricInfo.Name);
                //    }
                //}

                //this.Tracer.TraceDetail("Umids Count {0}", metricInfo.Umids.Count);
                //this.Tracer.TraceInfo("Distro Info {0}", metricInfo.DistroInfo);

                nodeMetricReported(this, new NodeMetricReportedEventArgs(metricInfo.Name, metricInfo.CoreCount, metricInfo.SocketCount, metricInfo.MemoryMegabytes)
                {
                     DistroInfo = metricInfo.DistroInfo,
                     NetworksInfo = metricInfo.NetworkInfo,
                });
            }

            Guid nodeid = GetNodeId(metricInfo.Name);
            if (nodeid == Guid.Empty)
            {
                this.Tracer.TraceWarning("Ignore MetricData from unknown Node {0}.", metricInfo.Name);
                return;
            }

            this.sender.SendData(nodeid, metricInfo.GetUmids(), metricInfo.GetValues(), metricInfo.TickCount);

            this.Tracer.TraceInfo("Send MetricData from Node {0} ({1}).", metricInfo.Name, nodeid);
        }

        private Guid GetNodeId(string nodeName)
        {
            Guid guid = Guid.Empty;
            if (string.IsNullOrEmpty(nodeName))
            {
                return guid;
            }

            string lower = nodeName.ToLower();
            if (this.nodeMap.ContainsKey(lower))
            {
                guid = this.nodeMap[lower];
            }
            else
            {
                // new node added ? refresh the nodemap
                using (IScheduler scheduler = new Scheduler.Scheduler())
                {
                    scheduler.Connect(this.HeadNode);
                    FilterCollection fc = new FilterCollection();
                    fc.Add(FilterOperator.Equal, NodePropertyIds.Location, NodeLocation.Linux);
                    foreach (ISchedulerNode node in scheduler.GetNodeList(fc, null))
                    {
                        string l = node.Name.ToLower();
                        if (!this.nodeMap.ContainsKey(l))
                        {
                            nodeMap.Add(l, node.Guid);
                        }
                        if (l.Equals(lower))
                        {
                            guid = node.Guid;
                        }
                    }

                    scheduler.Close();
                }
            }

            return guid;
        }
    }
}
