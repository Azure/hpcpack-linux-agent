using System.Net.Mime;

namespace NodeAgent;

public class ErrorHandler
{
    RequestDelegate _next;
    ILogger _logger;

    public ErrorHandler(RequestDelegate next, ILogger<ErrorHandler> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception!");

            var result = new { Exception = exception?.GetType().FullName, Message = exception?.ToString() };
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsJsonAsync(result);
        }
    }
}
