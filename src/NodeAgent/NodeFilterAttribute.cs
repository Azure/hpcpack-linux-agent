using Microsoft.AspNetCore.Mvc.Filters;

namespace NodeAgent;

public class NodeFilterAttribute : Attribute, IAsyncResourceFilter
{
    public Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        //TODO: Filter on route param node ...
        return next();
    }
}
