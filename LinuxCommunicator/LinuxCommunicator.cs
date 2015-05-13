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
using System.Xml.Linq;
using System.Security.Principal;

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

        private CancellationTokenSource cancellationTokenSource;

        private static LinuxCommunicator instance;
        private Lazy<string> headNodeFqdn;

        public LinuxCommunicator()
        {
            if (instance != null)
            {
                throw new InvalidOperationException("An instance of LinuxCommunicator already exists.");
            }

            instance = this;
            this.headNodeFqdn = new Lazy<string>(() => Dns.GetHostEntryAsync(this.HeadNode).Result.HostName, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public event EventHandler<RegisterEventArgs> RegisterRequested;

        public void Dispose()
        {
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

        public void SetTracer(INodeCommTracing tracer)
        {
            this.Tracer = tracer;
        }

        static bool IsAdmin(string userName, string password)
        {
            SafeToken token = Credentials.GetTokenFromCredentials(userName, password);
            WindowsIdentity identity = new WindowsIdentity(token.DangerousGetHandle());
            if (identity.IsSystem)
            {
                return true;
            }

            WindowsPrincipal principal = new WindowsPrincipal(identity);
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                return true;
            }

            return false;

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
            this.SendRequest("endjob", this.GetCallbackUri(nodeName, "taskcompleted"), nodeName, async (content, ex) =>
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
            this.SendRequest("endtask", this.GetCallbackUri(nodeName, "taskcompleted"), nodeName, async (content, ex) =>
            {
                Exception readEx = null;

                try
                {
                    arg.TaskInfo = await content.ReadAsAsync<ComputeClusterTaskInformation>();
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

        public void StartJobAndTask(string nodeName, StartJobAndTaskArg arg, string userName, string password, ProcessStartInfo startInfo, NodeCommunicatorCallBack<StartJobAndTaskArg> callback)
        {
            if (IsAdmin(userName, password))
            {
                startInfo.EnvironmentVariables["CCP_ISADMIN"] = "1";
            }

            this.SendRequest("startjobandtask", this.GetCallbackUri(nodeName, "taskcompleted"), nodeName, (content, ex) =>
            {
                callback(nodeName, arg, ex);
            }, Tuple.Create(arg, startInfo, userName, password));
        }

        public void StartJobAndTaskSoftCardCred(string nodeName, StartJobAndTaskArg arg, string userName, string password, byte[] certificate, ProcessStartInfo startInfo, NodeCommunicatorCallBack<StartJobAndTaskArg> callback)
        {
            this.SendRequest("startjobandtask", this.GetCallbackUri(nodeName, "taskcompleted"), nodeName, (content, ex) =>
            {
                callback(nodeName, arg, ex);
            }, Tuple.Create(arg, startInfo, userName, password, certificate));
        }

        public void StartJobAndTaskExtendedData(string nodeName, StartJobAndTaskArg arg, string userName, string password, string extendedData, ProcessStartInfo startInfo, NodeCommunicatorCallBack<StartJobAndTaskArg> callback)
        {
            string privateKey = null, publicKey = null;

            try
            {
                XDocument xDoc = XDocument.Parse(extendedData);

                var privateKeyNode = xDoc.Descendants("PrivateKey").FirstOrDefault();
                var publicKeyNode = xDoc.Descendants("PublicKey").FirstOrDefault();
                if (privateKeyNode != null)
                {
                    privateKey = privateKeyNode.Value;
                }

                if (publicKeyNode != null)
                {
                    publicKey = publicKeyNode.Value;
                }
            }
            catch (Exception ex)
            {
                this.Tracer.TraceWarning("Error parsing extended data {0}, ex {1}", extendedData, ex);
            }

            if (IsAdmin(userName, password))
            {
                startInfo.EnvironmentVariables["CCP_ISADMIN"] = "1";
            }

            if (startInfo.EnvironmentVariables.Contains("CCP_NODES"))
            {
                var nodes = startInfo.EnvironmentVariables["CCP_NODES"] as string;

                var tokens = nodes.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 1; i + 1 < tokens.Length; i += 2)
                {
                    int coreCount = 1;
                    var row = this.SchedulerCallbacks.GetNodeProperties(tokens[i], NodePropertyIds.NumCores);
                    var col = row.FirstOrDefault();
                    if (col != null)
                    {
                        coreCount = (int)col.Value;
                    }
                    else
                    {
                        this.Tracer.TraceWarning("Cannot get corecount for node {0}", nodeName);
                    }

                    tokens[i + 1] = coreCount.ToString();
                }

                startInfo.EnvironmentVariables["CCP_NODES_CORES"] = string.Join(" ", tokens);
                this.Tracer.TraceDetail("Converted CCP_NODES_CORES variable {0}", startInfo.EnvironmentVariables["CCP_NODES_CORES"]);
            }

            this.SendRequest("startjobandtask", this.GetCallbackUri(nodeName, "taskcompleted"), nodeName, (content, ex) =>
            {
                callback(nodeName, arg, ex);
            }, Tuple.Create(arg, startInfo, userName, password, privateKey, publicKey));
        }

        public void StartTask(string nodeName, StartTaskArg arg, ProcessStartInfo startInfo, NodeCommunicatorCallBack<StartTaskArg> callback)
        {
            this.SendRequest("starttask", this.GetCallbackUri(nodeName, "taskcompleted"), nodeName, (content, ex) =>
            {
                callback(nodeName, arg, ex);
            }, Tuple.Create(arg, startInfo));
        }

        public void Ping(string nodeName)
        {
            this.SendRequest<NodeCommunicatorCallBackArg>("ping", this.GetCallbackUri(nodeName, "computenodereported"), nodeName, (content, ex) =>
            {
                this.Tracer.TraceInfo("Compute node {0} pinged. Ex {1}", nodeName, ex);
            }, null);
        }

        public void SetMetricGuid(string nodeName, Guid nodeGuid)
        {
            var callbackUri = this.GetMetricCallbackUri(this.headNodeFqdn.Value, this.MonitoringPort, nodeGuid);
            this.SendRequest<NodeCommunicatorCallBackArg>("metric", callbackUri, nodeName, (content, ex) =>
            {
                this.Tracer.TraceInfo("Compute node {0} metric requested, callback {1}. Ex {2}", nodeGuid, callbackUri, ex);
            }, null);
        }

        private void SendRequest<T>(string action, string callbackUri, string nodeName, Action<HttpContent, Exception> callback, T arg)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, this.GetResoureUri(nodeName, action));
            request.Headers.Add(CallbackUriHeaderName, callbackUri);
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
            return new Uri(string.Format(ResourceUriFormat, nodeName, nodeName, action));
        }

        private string GetMetricCallbackUri(string headNodeName, int port, Guid nodeGuid)
        {
            return string.Format("udp://{0}:{1}/api/{2}/metricreported", headNodeName, port, nodeGuid);
        }

        private string GetCallbackUri(string nodeName, string action)
        {
            return string.Format("{0}/api/{1}/{2}", this.server.ListeningUri, nodeName, action);
        }

        public void OnRegisterRequested(RegisterEventArgs registerEventArgs)
        {
            var registerRequested = this.RegisterRequested;
            if (registerRequested != null)
            {
                registerRequested(this, registerEventArgs);
            }
        }
    }
}
