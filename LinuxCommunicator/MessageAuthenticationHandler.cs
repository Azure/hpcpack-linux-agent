using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Hpc.Communicators.LinuxCommunicator
{
    public class MessageAuthenticationHandler : DelegatingHandler
    {
        public const string AuthenticationHeaderKey = "AuthenticationKey";

        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string key = null;
            IEnumerable<string> values;
            if (request.Headers.TryGetValues(AuthenticationHeaderKey, out values))
            {
                key = values.FirstOrDefault();
            }

            if (!string.IsNullOrEmpty(key) || !string.IsNullOrEmpty(LinuxCommunicator.Instance.ClusterAuthenticationKey))
            {
                if (!string.Equals(key, LinuxCommunicator.Instance.ClusterAuthenticationKey, StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
                }
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
