using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Hpc.Activation;
using Microsoft.Hpc.Scheduler.Communicator;
using System.Net;
using Microsoft.Hpc.Scheduler.Properties;
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
        private const int AutoRetryLimit = 5;

        private readonly string HeadNode = (string)Microsoft.Win32.Registry.GetValue(HpcFullKeyName, ClusterNameKeyName, null);
        private readonly int MonitoringPort = 9894;
        private readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
        private readonly TimeSpan DelayBetweenRetry = TimeSpan.FromSeconds(3);

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
            this.client.Timeout = this.RequestTimeout;

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

        private void SendRequest<T>(string action, string callbackUri, string nodeName, Action<HttpContent, Exception> callback, T arg, int retryCount = 0)
        {
            this.Tracer.TraceDetail("Sending out request, action {0}, callback {1}, nodeName {2}", action, callbackUri, nodeName);
            var request = new HttpRequestMessage(HttpMethod.Post, this.GetResoureUri(nodeName, action));
            request.Headers.Add(CallbackUriHeaderName, callbackUri);
            var formatter = new JsonMediaTypeFormatter();
            request.Content = new ObjectContent<T>(arg, formatter);

            this.client.SendAsync(request, this.cancellationTokenSource.Token).ContinueWith(async t =>
            {
                Exception ex = t.Exception;
                this.Tracer.TraceDetail("Sending out request task completed, action {0}, callback {1}, nodeName {2} ex {3}", action, callbackUri, nodeName, ex);

                HttpContent content = null;

                if (ex == null)
                {
                    try
                    {
                        t.Result.EnsureSuccessStatusCode();
                        content = t.Result.Content;
                    }
                    catch (Exception e)
                    {
                        ex = e;
                    }
                }

                if (this.CanRetry(ex) && retryCount < AutoRetryLimit)
                {
                    await Task.Delay(DelayBetweenRetry);
                    this.SendRequest(action, callbackUri, nodeName, callback, arg, retryCount + 1);
                    return;
                }
                else
                {
                    callback(content, ex);
                }
            }, this.cancellationTokenSource.Token);
        }

        private bool CanRetry(Exception exception)
        {
            if (exception is HttpRequestException ||
                exception is WebException)
            {
                return true;
            }

            var aggregateEx = exception as AggregateException;
            if (aggregateEx != null)
            {
                aggregateEx = aggregateEx.Flatten();
                return aggregateEx.InnerExceptions.Any(e => this.CanRetry(e));
            }

            return false;
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
