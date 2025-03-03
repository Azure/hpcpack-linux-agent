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
    public async Task<IActionResult> StartJobAndTask(
        [FromHeader(Name = CallbackURIHeader)] string callbackURI,
        [FromBody] StartJobAndTaskArgsTuple argsTuple,
        [FromServices] IJobTaskFilter filter,
        [FromServices] IJobTaskExecutor executor,
        CancellationToken cancellationToken)
    {
        //TODO: Make sure the log can only be read by system admin like root since it may contain password.
        _logger.LogDebug("StartJobAndTask: before filter: {args}", argsTuple);

        /*
         * NOTE
         *
         * Here we pass the deserialized args object instead of the original JSON string to the filter.
         * In the filter, the object will be serialized again to string and the string is passed to a
         * user defined filter. This implies the user filter must tolerate the subtlety between different
         * JSON deserializers, that is, the one on the head node and the one used here in agent. The
         * subtlety is the property name casing. A user filter should ignore the case of property name.
         * This is by design. So is for StartTask.
         */
        argsTuple = await filter.OnJobStart(argsTuple);
        _logger.LogDebug("StartJobAndTask: after filter: {args}", argsTuple);

        await executor.StartJobAndTaskAsync(argsTuple.ToStartJobAndTaskArgs(), callbackURI, cancellationToken);
        return Ok();
    }

    [HttpPost("EndJob")]
    public async Task<IReadOnlyJobInfo?> EndJob(
        [FromBody] EndJobArgs args,
        [FromServices] IJobTaskFilter filter,
        [FromServices] IJobTaskExecutor executor,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("EndJob: before filter: {args}", args);
        args = await filter.OnJobEnd(args);
        _logger.LogDebug("EndJob: after filter: {args}", args);

        return await executor.EndJobAsync(args, cancellationToken);
    }

    [HttpPost("StartTask")]
    public async Task<IActionResult> StartTask(
        [FromHeader(Name = CallbackURIHeader)] string callbackURI,
        [FromBody] StartTaskArgsTuple argsTuple,
        [FromServices] IJobTaskFilter filter,
        [FromServices] IJobTaskExecutor executor,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("StartTask: before filter: {args}", argsTuple);
        argsTuple = await filter.OnTaskStart(argsTuple);
        _logger.LogDebug("StartTask: after filter: {args}", argsTuple);

        await executor.StartTaskAsync(argsTuple.ToStartTaskArgs(), callbackURI, cancellationToken);
        return Ok();
    }

    [HttpPost("EndTask")]
    public async Task<IReadOnlyTaskInfo?> EndTask(
        [FromHeader(Name = CallbackURIHeader)] string callbackURI,
        [FromBody] EndTaskArgs args,
        [FromServices] IJobTaskExecutor executor,
        CancellationToken cancellationToken)
    {
        return await executor.EndTaskAsync(args, callbackURI, cancellationToken);
    }

    [HttpPost("PeekTaskOutput")]
    public async Task<IActionResult> PeekTaskOutput(
        [FromBody] PeekTaskOutputArgs args,
        [FromServices] IJobTaskExecutor executor,
        CancellationToken cancellationToken)
    {
        var result = await executor.PeekTaskOutputAsync(args, cancellationToken);
        return Ok(result);
    }

    //TODO/Q: How to trigger a ping request?
    [HttpPost("Ping")]
    public async Task<IActionResult> Ping(
        [FromHeader(Name = CallbackURIHeader)] string callbackURI,
        [FromServices] IHeartbeatService heartbeat,
        CancellationToken cancellationToken)
    {
        await heartbeat.PingAsync(callbackURI, cancellationToken);
        return Ok();
    }

    [HttpPost("Metric")]
    public IActionResult Metric(
        [FromHeader(Name = CallbackURIHeader)] string? callbackURI,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    [HttpPost("MetricConfig")]
    public IActionResult MetricConfig(
        [FromHeader(Name = CallbackURIHeader)] string? callbackURI,
        [FromBody] MetricCountersConfig config,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
