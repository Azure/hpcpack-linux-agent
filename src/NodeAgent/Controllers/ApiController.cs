using Microsoft.AspNetCore.Mvc;
using NodeAgent.Models;

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
        [FromBody] StartJobAndTaskArgs args)
    {
        throw new NotImplementedException();
    }

    [HttpPost("EndJob")]
    public IActionResult EndJob(
        [FromHeader(Name = CallbackURIHeader)] string? callbackURI,
        [FromBody] EndJobArgs args)
    {
        throw new NotImplementedException();
    }

    [HttpPost("StartTask")]
    public IActionResult StartTask(
        [FromHeader(Name = CallbackURIHeader)] string? callbackURI,
        [FromBody] StartTaskArgs args)
    {
        throw new NotImplementedException();
    }

    [HttpPost("EndTask")]
    public IActionResult EndTask(
        [FromHeader(Name = CallbackURIHeader)] string? callbackURI,
        [FromBody] EndTaskArgs args)
    {
        throw new NotImplementedException();
    }

    [HttpPost("PeekTaskOutput")]
    public IActionResult PeekTaskOutput([FromBody] PeekTaskOutputArgs args)
    {
        throw new NotImplementedException();
    }

    [HttpPost("Ping")]
    public IActionResult Ping([FromHeader(Name = CallbackURIHeader)] string? callbackURI)
    {
        throw new NotImplementedException();
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
