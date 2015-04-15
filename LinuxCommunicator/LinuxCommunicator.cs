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
using System.Collections.Concurrent;

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
        private readonly TimeSpan ResyncPeriod = TimeSpan.FromSeconds(30.0);

        private HttpClient client;
        private WebServer server;
        private Monitoring.CounterDataSender sender;
        private ConcurrentDictionary<string, Guid> nodeMap;

        private CancellationTokenSource cancellationTokenSource;

        private static LinuxCommunicator instance;

        private Timer resyncNodeGuid;

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

            this.resyncNodeGuid = new Timer(o =>
            {
                this.nodeMap.Clear();
            }, null, ResyncPeriod, ResyncPeriod);

            using (IScheduler scheduler = new Scheduler.Scheduler())
            {
                scheduler.Connect(this.HeadNode);
                this.nodeMap = new ConcurrentDictionary<string, Guid>();
                FilterCollection fc = new FilterCollection();
                fc.Add(FilterOperator.Equal, NodePropertyIds.Location, NodeLocation.Linux);
                foreach (ISchedulerNode node in scheduler.GetNodeList(fc, null))
                {
                    this.nodeMap.TryAdd(node.Name.ToLower(), node.Guid);
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
            string privateKey = @"-----BEGIN RSA PRIVATE KEY-----
MIIEpQIBAAKCAQEAxJKBABhnOsE9eneGHvsjdoXKooHUxpTHI1JVunAJkVmFy8JC
qFt1pV98QCtKEHTC6kQ7tj1UT2N6nx1EY9BBHpZacnXmknpKdX4Nu0cNlSphLpru
lscKPR3XVzkTwEF00OMiNJVknq8qXJF1T3lYx3rW5EnItn6C3nQm3gQPXP0ckYCF
Jdtu/6SSgzV9kaapctLGPNp1Vjf9KeDQMrJXsQNHxnQcfiICp21NiUCiXosDqJrR
AfzePdl0XwsNngouy8t0fPlNSngZvsx+kPGh/AKakKIYS0cO9W3FmdYNW8Xehzkc
VzrtJhU8x21hXGfSC7V0ZeD7dMeTL3tQCVxCmwIDAQABAoIBAQCve8Jh3Wc6koxZ
qh43xicwhdwSGyliZisoozYZDC/ebDb/Ydq0BYIPMiDwADVMX5AqJuPPmwyLGtm6
9hu5p46aycrQ5+QA299g6DlF+PZtNbowKuvX+rRvPxagrTmupkCswjglDUEYUHPW
05wQaNoSqtzwS9Y85M/b24FfLeyxK0n8zjKFErJaHdhVxI6cxw7RdVlSmM9UHmah
wTkW8HkblbOArilAHi6SlRTNZG4gTGeDzPb7fYZo3hzJyLbcaNfJscUuqnAJ+6pT
iY6NNp1E8PQgjvHe21yv3DRoVRM4egqQvNZgUbYAMUgr30T1UoxnUXwk2vqJMfg2
Nzw0ESGRAoGBAPkfXjjGfc4HryqPkdx0kjXs0bXC3js2g4IXItK9YUFeZzf+476y
OTMQg/8DUbqd5rLv7PITIAqpGs39pkfnyohPjOe2zZzeoyaXurYIPV98hhH880uH
ZUhOxJYnlqHGxGT7p2PmmnAlmY4TSJrp12VnuiQVVVsXWOGPqHx4S4f9AoGBAMn/
vuea7hsCgwIE25MJJ55FYCJodLkioQy6aGP4NgB89Azzg527WsQ6H5xhgVMKHWyu
Q1snp+q8LyzD0i1veEvWb8EYifsMyTIPXOUTwZgzaTTCeJNHdc4gw1U22vd7OBYy
nZCU7Tn8Pe6eIMNztnVduiv+2QHuiNPgN7M73/x3AoGBAOL0IcmFgy0EsR8MBq0Z
ge4gnniBXCYDptEINNBaeVStJUnNKzwab6PGwwm6w2VI3thbXbi3lbRAlMve7fKK
B2ghWNPsJOtppKbPCek2Hnt0HUwb7qX7Zlj2cX/99uvRAjChVsDbYA0VJAxcIwQG
TxXx5pFi4g0HexCa6LrkeKMdAoGAcvRIACX7OwPC6nM5QgQDt95jRzGKu5EpdcTf
g4TNtplliblLPYhRrzokoyoaHteyxxak3ktDFCLj9eW6xoCZRQ9Tqd/9JhGwrfxw
MS19DtCzHoNNewM/135tqyD8m7pTwM4tPQqDtmwGErWKj7BaNZCRUlhFxwOoemsv
R6DbZyECgYEAhjL2N3Pc+WW+8x2bbIBN3rJcMjBBIivB62AwgYZnA2D5wk5o0DKD
eesGSKS5l22ZMXJNShgzPKmv3HpH22CSVpO0sNZ6R+iG8a3oq4QkU61MT1CfGoMI
a8lxTKnZCsRXU1HexqZs+DSc+30tz50bNqLdido/l5B4EJnQP03ciO0=
-----END RSA PRIVATE KEY-----";
            string publicKey = @"ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQDEkoEAGGc6wT16d4Ye+yN2hcqigdTGlMcjUlW6cAmRWYXLwkKoW3WlX3xAK0oQdMLqRDu2PVRPY3qfHURj0EEellpydeaSekp1fg27Rw2VKmEumu6Wxwo9HddXORPAQXTQ4yI0lWSerypckXVPeVjHetbkSci2foLedCbeBA9c/RyRgIUl227/pJKDNX2Rpqly0sY82nVWN/0p4NAyslexA0fGdBx+IgKnbU2JQKJeiwOomtEB/N492XRfCw2eCi7Ly3R8+U1KeBm+zH6Q8aH8ApqQohhLRw71bcWZ1g1bxd6HORxXOu0mFTzHbWFcZ9ILtXRl4Pt0x5Mve1AJXEKb hpclabsa@longhaulLN5-033\n";

            this.SendRequest("startjobandtask", "taskcompleted", nodeName, (content, ex) =>
            {
                callback(nodeName, arg, ex);
            }, Tuple.Create(arg, startInfo, userName, password, privateKey, publicKey));
        }

        public void StartJobAndTaskSoftCardCred(string nodeName, StartJobAndTaskArg arg, string userName, string password, byte[] certificate, ProcessStartInfo startInfo, NodeCommunicatorCallBack<StartJobAndTaskArg> callback)
        {
            string privateKey = @"-----BEGIN RSA PRIVATE KEY-----
MIIEpQIBAAKCAQEAxJKBABhnOsE9eneGHvsjdoXKooHUxpTHI1JVunAJkVmFy8JC
qFt1pV98QCtKEHTC6kQ7tj1UT2N6nx1EY9BBHpZacnXmknpKdX4Nu0cNlSphLpru
lscKPR3XVzkTwEF00OMiNJVknq8qXJF1T3lYx3rW5EnItn6C3nQm3gQPXP0ckYCF
Jdtu/6SSgzV9kaapctLGPNp1Vjf9KeDQMrJXsQNHxnQcfiICp21NiUCiXosDqJrR
AfzePdl0XwsNngouy8t0fPlNSngZvsx+kPGh/AKakKIYS0cO9W3FmdYNW8Xehzkc
VzrtJhU8x21hXGfSC7V0ZeD7dMeTL3tQCVxCmwIDAQABAoIBAQCve8Jh3Wc6koxZ
qh43xicwhdwSGyliZisoozYZDC/ebDb/Ydq0BYIPMiDwADVMX5AqJuPPmwyLGtm6
9hu5p46aycrQ5+QA299g6DlF+PZtNbowKuvX+rRvPxagrTmupkCswjglDUEYUHPW
05wQaNoSqtzwS9Y85M/b24FfLeyxK0n8zjKFErJaHdhVxI6cxw7RdVlSmM9UHmah
wTkW8HkblbOArilAHi6SlRTNZG4gTGeDzPb7fYZo3hzJyLbcaNfJscUuqnAJ+6pT
iY6NNp1E8PQgjvHe21yv3DRoVRM4egqQvNZgUbYAMUgr30T1UoxnUXwk2vqJMfg2
Nzw0ESGRAoGBAPkfXjjGfc4HryqPkdx0kjXs0bXC3js2g4IXItK9YUFeZzf+476y
OTMQg/8DUbqd5rLv7PITIAqpGs39pkfnyohPjOe2zZzeoyaXurYIPV98hhH880uH
ZUhOxJYnlqHGxGT7p2PmmnAlmY4TSJrp12VnuiQVVVsXWOGPqHx4S4f9AoGBAMn/
vuea7hsCgwIE25MJJ55FYCJodLkioQy6aGP4NgB89Azzg527WsQ6H5xhgVMKHWyu
Q1snp+q8LyzD0i1veEvWb8EYifsMyTIPXOUTwZgzaTTCeJNHdc4gw1U22vd7OBYy
nZCU7Tn8Pe6eIMNztnVduiv+2QHuiNPgN7M73/x3AoGBAOL0IcmFgy0EsR8MBq0Z
ge4gnniBXCYDptEINNBaeVStJUnNKzwab6PGwwm6w2VI3thbXbi3lbRAlMve7fKK
B2ghWNPsJOtppKbPCek2Hnt0HUwb7qX7Zlj2cX/99uvRAjChVsDbYA0VJAxcIwQG
TxXx5pFi4g0HexCa6LrkeKMdAoGAcvRIACX7OwPC6nM5QgQDt95jRzGKu5EpdcTf
g4TNtplliblLPYhRrzokoyoaHteyxxak3ktDFCLj9eW6xoCZRQ9Tqd/9JhGwrfxw
MS19DtCzHoNNewM/135tqyD8m7pTwM4tPQqDtmwGErWKj7BaNZCRUlhFxwOoemsv
R6DbZyECgYEAhjL2N3Pc+WW+8x2bbIBN3rJcMjBBIivB62AwgYZnA2D5wk5o0DKD
eesGSKS5l22ZMXJNShgzPKmv3HpH22CSVpO0sNZ6R+iG8a3oq4QkU61MT1CfGoMI
a8lxTKnZCsRXU1HexqZs+DSc+30tz50bNqLdido/l5B4EJnQP03ciO0=
-----END RSA PRIVATE KEY-----";
            string publicKey = @"ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQDEkoEAGGc6wT16d4Ye+yN2hcqigdTGlMcjUlW6cAmRWYXLwkKoW3WlX3xAK0oQdMLqRDu2PVRPY3qfHURj0EEellpydeaSekp1fg27Rw2VKmEumu6Wxwo9HddXORPAQXTQ4yI0lWSerypckXVPeVjHetbkSci2foLedCbeBA9c/RyRgIUl227/pJKDNX2Rpqly0sY82nVWN/0p4NAyslexA0fGdBx+IgKnbU2JQKJeiwOomtEB/N492XRfCw2eCi7Ly3R8+U1KeBm+zH6Q8aH8ApqQohhLRw71bcWZ1g1bxd6HORxXOu0mFTzHbWFcZ9ILtXRl4Pt0x5Mve1AJXEKb hpclabsa@longhaulLN5-033\n";

            this.SendRequest("startjobandtask", "taskcompleted", nodeName, (content, ex) =>
            {
                callback(nodeName, arg, ex);
            }, Tuple.Create(arg, startInfo, userName, password, privateKey, publicKey));
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

        private void SendRequest<T>(string action, string callbackAction, string nodeName, Action<HttpContent, Exception> callback, T arg)
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
            if (!this.nodeMap.TryGetValue(lower, out guid))
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
                        this.nodeMap.AddOrUpdate(l, node.Guid, (k, g) => node.Guid);

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
