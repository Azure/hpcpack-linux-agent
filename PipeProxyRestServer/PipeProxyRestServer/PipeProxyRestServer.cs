using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Microsoft.Hpc.Scheduler.PipeProxy
{
    public class RestServer
    {
        public class Startup
        {
            public void Configuration(IAppBuilder appBuilder)
            {
                HttpConfiguration config = new HttpConfiguration();
                config.MapHttpAttributeRoutes();
                config.Routes.MapHttpRoute(
                    name: "DefaultApi",
                    routeTemplate: "api/{controller}/{id}",
                    defaults: new { id = RouteParameter.Optional }
                );

                appBuilder.UseWebApi(config);
            }
        }

        public class MessageController : ApiController
        {
            [HttpPost]
            [Route("api/message")]
            public void Post([FromBody] ClusrunOutputFromLinux restmessage)
            {
                RestMessageRepository.Instance.Put(restmessage);
            }
        }

        public IDisposable StartServer(string baseAddress)
        {
            return WebApp.Start<Startup>(url: baseAddress);
        }
    }
}
