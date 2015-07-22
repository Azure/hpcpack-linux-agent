using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Hpc.Scheduler.Communicator;
using Microsoft.Owin.Hosting;
using Microsoft.ComputeCluster.Management.Win32Helpers;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator
{
    internal class WebServer : IDisposable
    {
        private CancellationTokenSource source = new CancellationTokenSource();

        public const string LinuxCommunicatorUriTemplate = "http://{0}:40001";

        public WebServer()
        {
            var nonDirectNetIp = Dns.GetHostEntry(Environment.MachineName).AddressList
                .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !NetDirectConfiguration.IsNDEnabled(a.GetAddressBytes()) && !IPAddress.IsLoopback(a))
                .FirstOrDefault();

            if (nonDirectNetIp == null)
            {
                throw new InvalidOperationException("The current system doesn't have a non-NDMA network");
            }

            this.ListeningUri = string.Format(LinuxCommunicatorUriTemplate, "*");
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public string ListeningUri
        {
            get;
            private set;
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing )
            {
                this.Stop();
            }
        }

        public async Task Start()
        {
            await Task.Yield();

            if (this.source != null) { this.source.Dispose(); }
            this.source = new CancellationTokenSource();

            LinuxCommunicator.Instance.Tracer.TraceInfo("Start listening on {0}", this.ListeningUri);
            
            var webApp = WebApp.Start<Startup>(this.ListeningUri);
            this.source.Token.Register(() =>
            {
                webApp.Dispose();
                LinuxCommunicator.Instance.Tracer.TraceInfo("Stop listening on {0}", this.ListeningUri);
            });
        }

        public void Stop()
        {
            if (this.source != null)
            {
                this.source.Cancel();
                this.source.Dispose();
                this.source = null;
            }
        }
    }
}
