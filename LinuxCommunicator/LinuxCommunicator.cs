using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Security;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Hpc.Activation;
using Microsoft.Hpc.Communicators.LinuxCommunicator.HostsFile;
using Microsoft.Hpc.Communicators.LinuxCommunicator.Monitoring;
using Microsoft.Hpc.Scheduler.Communicator;
using Microsoft.Hpc.Scheduler.Properties;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator
{
    public class LinuxCommunicator : IManagedResourceCommunicator, IDisposable
    {
        private const string HttpsResourceUriFormat = "https://{0}:40002/api/{1}/{2}";
        private const string HttpResourceUriFormat = "http://{0}:40000/api/{1}/{2}";
        private const string CallbackUriHeaderName = "CallbackUri";
        private const string HpcFullKeyName = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\HPC";
        private const string ClusterNameKeyName = "ClusterName";
        private const string ClusterAuthenticationKeyName = "ClusterAuthenticationKey";
        private const string LinuxHttpsKeyName = "LinuxHttps";
        private const int AutoRetrySendLimit = 3;
        private const int AutoRetryStartLimit = 3;

        public readonly string ClusterAuthenticationKey = (string)Microsoft.Win32.Registry.GetValue(HpcFullKeyName, ClusterAuthenticationKeyName, null);
        public readonly int IsHttps = (int)Microsoft.Win32.Registry.GetValue(HpcFullKeyName, LinuxHttpsKeyName, 0);
        private readonly string HeadNode = (string)Microsoft.Win32.Registry.GetValue(HpcFullKeyName, ClusterNameKeyName, null);
        private readonly int MonitoringPort = 9894;
        private readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(40);
        private readonly TimeSpan DelayBetweenRetry = TimeSpan.FromSeconds(3);
        private readonly TimeSpan RetryStartInterval = TimeSpan.FromSeconds(10);

        private WebServer server;

        private CancellationTokenSource cancellationTokenSource;

        private static LinuxCommunicator instance;
        private Lazy<string> headNodeFqdn;

        private ConcurrentDictionary<string, Guid> cachedNodeGuids = new ConcurrentDictionary<string, Guid>();

        private string ResourceUriFormat { get { return this.IsHttps > 0 ? HttpsResourceUriFormat : HttpResourceUriFormat; } }

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

        public MonitoringConfigManager MonitoringConfigManager { get; private set; }

        public HostsFileManager HostsManager { get; private set; }

        public void Dispose()
        {
            this.server.Dispose();
            this.MonitoringConfigManager.Dispose();
            this.HostsManager.Dispose();
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

            this.MonitoringConfigManager = new MonitoringConfigManager();
            Task.Run(() => this.MonitoringConfigManager.Initialize(this.headNodeFqdn.Value));
            this.HostsManager = new HostsFileManager();

            ServicePointManager.ServerCertificateValidationCallback += (s, cert, chain, sslPolicyErrors) =>
            {
                this.Tracer.TraceDetail("sslPolicyErrors {0}", sslPolicyErrors);
                sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;

                return sslPolicyErrors == System.Net.Security.SslPolicyErrors.None;
            };

            this.server = new WebServer(this.IsHttps > 0);

            if (this.HeadNode == null)
            {
                ArgumentNullException exp = new ArgumentNullException(ClusterNameKeyName);
                Tracer.TraceError("Failed to find registry value: {0}. {1}", ClusterNameKeyName, exp);
                throw exp;
            }

            this.MonitoringConfigManager.ConfigChanged += (s, e) =>
            {
                var result = Parallel.ForEach(this.cachedNodeGuids, kvp =>
                {
                    this.SetMetricConfig(kvp.Key, kvp.Value, e.CurrentConfig);
                });
            };

            this.Tracer.TraceInfo("Initialized LinuxCommunicator.");
            return true;
        }

        public bool Start()
        {
            this.MonitoringConfigManager.Start();
            return this.Start(0);
        }

        private bool Start(int retryCount)
        {
            this.Tracer.TraceInfo("Starting LinuxCommunicator. RetryCount {0}", retryCount);

            if (retryCount >= AutoRetryStartLimit)
            {
                this.Tracer.TraceInfo("Exceeding the auto retry start limit {0}", AutoRetryStartLimit);

                return false;
            }

            if (this.cancellationTokenSource != null) { this.cancellationTokenSource.Dispose(); }
            this.cancellationTokenSource = new CancellationTokenSource();

            try
            {
                this.server.Start().Wait();
            }
            catch (AggregateException aggrEx)
            {
                if (aggrEx.InnerExceptions.Any(e => e is HttpListenerException))
                {
                    this.Tracer.TraceWarning("Failed to start http listener {0}", aggrEx);
                    return this.Start(retryCount + 1);
                }

                throw;
            }
            catch (HttpListenerException ex)
            {
                this.Tracer.TraceWarning("Failed to start http listener {0}", ex);
                return this.Start(retryCount + 1);
            }

            return true;
        }

        public bool Stop()
        {
            this.Tracer.TraceInfo("Stopping LinuxCommunicator.");
            this.server.Stop();
            this.MonitoringConfigManager.Stop();
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
                    if (content != null && ex == null)
                    {
                        arg.JobInfo = await content.ReadAsAsync<ComputeClusterJobInformation>();
                    }
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
                    if (content != null && ex == null)
                    {
                        arg.TaskInfo = await content.ReadAsAsync<ComputeClusterTaskInformation>();
                    }
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

            this.SendRequest("startjobandtask", this.GetCallbackUri(nodeName, "taskcompleted"), nodeName, async (content, ex) =>
            {
                await Task.Yield();
                callback(nodeName, arg, ex);
            }, Tuple.Create(arg, startInfo, userName, password));
        }

        public void StartJobAndTaskSoftCardCred(string nodeName, StartJobAndTaskArg arg, string userName, string password, byte[] certificate, ProcessStartInfo startInfo, NodeCommunicatorCallBack<StartJobAndTaskArg> callback)
        {
            this.SendRequest("startjobandtask", this.GetCallbackUri(nodeName, "taskcompleted"), nodeName, async (content, ex) =>
            {
                await Task.Yield();
                callback(nodeName, arg, ex);
            }, Tuple.Create(arg, startInfo, userName, password, certificate));
        }

        public void StartJobAndTaskExtendedData(string nodeName, StartJobAndTaskArg arg, string userName, string password, string extendedData, ProcessStartInfo startInfo, NodeCommunicatorCallBack<StartJobAndTaskArg> callback)
        {
            string privateKey = null, publicKey = null;

            if (extendedData != null)
            {
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
            }

            if (IsAdmin(userName, password))
            {
                startInfo.EnvironmentVariables["CCP_ISADMIN"] = "1";
            }

            this.SendRequest("startjobandtask", this.GetCallbackUri(nodeName, "taskcompleted"), nodeName, async (content, ex) =>
            {
                await Task.Yield();
                callback(nodeName, arg, ex);
            }, Tuple.Create(arg, startInfo, userName, password, privateKey, publicKey));
        }

        public void StartTask(string nodeName, StartTaskArg arg, ProcessStartInfo startInfo, NodeCommunicatorCallBack<StartTaskArg> callback)
        {
            this.SendRequest("starttask", this.GetCallbackUri(nodeName, "taskcompleted"), nodeName, async (content, ex) =>
            {
                await Task.Yield();
                callback(nodeName, arg, ex);
            }, Tuple.Create(arg, startInfo));
        }

        public void Ping(string nodeName)
        {
            this.SendRequest<NodeCommunicatorCallBackArg>("ping", this.GetCallbackUri(nodeName, "computenodereported"), nodeName, async (content, ex) =>
            {
                await Task.Yield();
                this.Tracer.TraceInfo("Compute node {0} pinged. Ex {1}", nodeName, ex);
            }, null);
        }

        public void SetMetricGuid(string nodeName, Guid nodeGuid)
        {
            this.cachedNodeGuids.AddOrUpdate(nodeName, nodeGuid, (s, g) => nodeGuid);
            this.SetMetricConfig(nodeName, nodeGuid, this.MonitoringConfigManager.MetricCountersConfig);
        }

        public void SetMetricConfig(string nodeName, Guid nodeGuid, MetricCountersConfig config)
        {
            var callbackUri = this.GetMetricCallbackUri(this.headNodeFqdn.Value, this.MonitoringPort, nodeGuid);
            this.SendRequest("metricconfig", callbackUri, nodeName, async (content, ex) =>
            {
                await Task.Yield();
                this.Tracer.TraceInfo("Compute node {0} metricconfig requested, callback {1}. Ex {2}", nodeGuid, callbackUri, ex);
            }, config);

            this.SendRequest("metric", callbackUri, nodeName, async (content, ex) =>
            {
                await Task.Yield();
                this.Tracer.TraceInfo("Compute node {0} metric requested, callback {1}. Ex {2}", nodeGuid, callbackUri, ex);
            }, config);
        }

        private async Task SendRequestInternal<T>(string action, string callbackUri, string nodeName, Func<HttpContent, Exception, Task> callback, T arg, int retryCount = 0)
        {
            this.Tracer.TraceDetail("Sending out request, action {0}, callback {1}, nodeName {2}", action, callbackUri, nodeName);
            var request = new HttpRequestMessage(HttpMethod.Post, this.GetResoureUri(nodeName, action));
            request.Headers.Add(CallbackUriHeaderName, callbackUri);
            request.Headers.Add(MessageAuthenticationHandler.AuthenticationHeaderKey, this.ClusterAuthenticationKey);
            var formatter = new JsonMediaTypeFormatter();
            request.Content = new ObjectContent<T>(arg, formatter);

            Exception ex = null;
            HttpContent content = null;
            bool retry = false;

            using (HttpClient client = new HttpClient())
            {
                client.Timeout = this.RequestTimeout;

                HttpResponseMessage response = null;
                try
                {
                    response = await client.SendAsync(request, this.cancellationTokenSource.Token);
                }
                catch (Exception e)
                {
                    ex = e;
                }

                this.Tracer.TraceDetail("Sending out request task completed, action {0}, callback {1}, nodeName {2} ex {3}", action, callbackUri, nodeName, ex);

                if (ex == null)
                {
                    try
                    {
                        content = response.Content;

                        if (!response.IsSuccessStatusCode)
                        {
                            using (content)
                            {
                                if (response.StatusCode >= HttpStatusCode.InternalServerError)
                                {
                                    throw new InvalidProgramException(await content.ReadAsStringAsync());
                                }
                                else
                                {
                                    response.EnsureSuccessStatusCode();
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        ex = e;
                    }
                }

                if (this.CanRetry(ex) && retryCount < AutoRetrySendLimit)
                {
                    retry = true;
                }
                else
                {
                    try
                    {
                        await callback(content, ex);
                    }
                    catch (Exception callbackEx)
                    {
                        this.Tracer.TraceError("Finished sending, callback error: action {0}, callback {1}, nodeName {2} retry count {3}, ex {4}", action, callbackUri, nodeName, retryCount, callbackEx);
                    }
                }
            }

            if (retry)
            {
                await Task.Delay(DelayBetweenRetry);
                this.SendRequest(action, callbackUri, nodeName, callback, arg, retryCount + 1);
            }
        }

        private void SendRequest<T>(string action, string callbackUri, string nodeName, Func<HttpContent, Exception, Task> callback, T arg, int retryCount = 0)
        {
            this.SendRequestInternal(action, callbackUri, nodeName, callback, arg, retryCount).ContinueWith(t =>
            {
                this.Tracer.TraceDetail("Finished sending, action {0}, callback {1}, nodeName {2} retry count {3}", action, callbackUri, nodeName, retryCount);
            });
        }

        private bool CanRetry(Exception exception)
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return false;
            }

            if (exception is HttpRequestException ||
                exception is WebException ||
                exception is TaskCanceledException)
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
            return new Uri(string.Format(this.ResourceUriFormat, nodeName, nodeName, action));
        }

        private string GetMetricCallbackUri(string headNodeName, int port, Guid nodeGuid)
        {
            return string.Format("udp://{0}:{1}/api/{2}/metricreported", headNodeName, port, nodeGuid);
        }

        private string GetCallbackUri(string nodeName, string action)
        {
            return string.Format("{0}/api/{1}/{2}", string.Format(CultureInfo.InvariantCulture, this.server.LinuxCommunicatorUriTemplate, this.headNodeFqdn.Value), nodeName, action);
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
