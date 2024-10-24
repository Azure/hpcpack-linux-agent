using Microsoft.AspNetCore.Mvc;
using NodeAgent.Models;
using NodeAgent.Services;

namespace NodeAgent.Controllers;

[ApiController]
[Route("/api/{node}")]
[NodeFilter]
public class ApiController : ControllerBase
{
    public const string CallbackURIHeader = "CallbackURI";

    private readonly ILogger _logger;

    public ApiController(ILogger<ApiController> logger)
    {
        _logger = logger;
    }

    [HttpPost("StartJobAndTask")]
    public IActionResult StartJobAndTask(
        [FromHeader(Name = CallbackURIHeader)] string? callbackURI,
        [FromBody] StartJobAndTaskArgs args,
        [FromServices] IJobTaskExecutor executor)
    {
        throw new NotImplementedException();
    }

    [HttpPost("EndJob")]
    public IActionResult EndJob(
        [FromHeader(Name = CallbackURIHeader)] string? callbackURI,
        [FromBody] EndJobArgs args,
        [FromServices] IJobTaskExecutor executor)
    {
        throw new NotImplementedException();
    }

    [HttpPost("StartTask")]
    public IActionResult StartTask(
        [FromHeader(Name = CallbackURIHeader)] string? callbackURI,
        [FromBody] StartTaskArgs args,
        [FromServices] IJobTaskExecutor executor)
    {
        throw new NotImplementedException();
    }

    [HttpPost("EndTask")]
    public IActionResult EndTask(
        [FromHeader(Name = CallbackURIHeader)] string? callbackURI,
        [FromBody] EndTaskArgs args,
        [FromServices] IJobTaskExecutor executor)
    {
        throw new NotImplementedException();
    }

    [HttpPost("PeekTaskOutput")]
    public IActionResult PeekTaskOutput(
        [FromBody] PeekTaskOutputArgs args,
        [FromServices] IJobTaskExecutor executor)
    {
        throw new NotImplementedException();
    }

    //TODO/Q: How to trigger a ping request?
    [HttpPost("Ping")]
    public async Task<IActionResult> Ping(
        [FromHeader(Name = CallbackURIHeader)] string callbackURI,
        [FromServices] IHeartbeatService heartbeat)
    {
        await heartbeat.PingAsync(callbackURI);
        return Ok();
    }

    [HttpPost("Metric")]
    public IActionResult Metric([FromHeader(Name = CallbackURIHeader)] string? callbackURI)
    {
        throw new NotImplementedException();
    }

    [HttpPost("MetricConfig")]
    public IActionResult MetricConfig(
        [FromHeader(Name = CallbackURIHeader)] string? callbackURI,
        [FromBody] MetricCountersConfig config)
    {
        throw new NotImplementedException();
    }
}
