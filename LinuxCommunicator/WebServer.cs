using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Hpc.Scheduler.Communicator;
using Microsoft.Owin.Hosting;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator
{
    internal class WebServer : IDisposable
    {
        private CancellationTokenSource source = new CancellationTokenSource();

        public const string LinuxCommunicatorUriTemplate = "http://{0}:50000";

        public WebServer()
        {
            var fqdn = Dns.GetHostEntry(Environment.MachineName).AddressList.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).First().ToString();
            this.ListeningUri = string.Format(LinuxCommunicatorUriTemplate, fqdn);
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
