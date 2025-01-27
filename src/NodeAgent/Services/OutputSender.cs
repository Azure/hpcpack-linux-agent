using NodeAgent.Models;

namespace NodeAgent.Services;

public interface IOutputSender
{
    int Order { get; }

    string Uri { get; }

    Task SendAsync(string data, CancellationToken cancellationToken = default);

    Task SendEndAsync(CancellationToken cancellationToken = default);
}

public class OutputSender : IOutputSender
{
    private ILogger? _logger;
    private HttpClient _httpClient;
    private string _hostName;
    private int _order = -1;
    private int _end = 0;

    public int Order => _order;

    public bool IsEnd => _end != 0;

    public string Uri { get; private set; }

    public OutputSender(ILogger<OutputSender>? logger, HttpClient httpClient, string uri, string hostName)
    {
        _logger = logger;
        _httpClient = httpClient;
        Uri = uri;
        _hostName = hostName;
    }

    private async Task SendDataAsync(OutputData data, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(Uri, data, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error when sending output to '{uri}'", Uri);
        }
    }

    public Task SendAsync(string content, CancellationToken cancellationToken = default)
    {
        if (IsEnd)
        {
            throw new InvalidOperationException();
        }
        var order = Interlocked.Increment(ref _order);
        var data = new OutputData() { Content = content, Order = order, NodeName = _hostName };
        return SendDataAsync(data, cancellationToken);
    }

    public Task SendEndAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _end) == 1)
        {
            var order = Interlocked.Increment(ref _order);
            var data = new OutputData() { Content = string.Empty, Order = order, NodeName = _hostName, Eof = true };
            return SendDataAsync(data, cancellationToken);
        }
        return Task.CompletedTask;
    }
}
