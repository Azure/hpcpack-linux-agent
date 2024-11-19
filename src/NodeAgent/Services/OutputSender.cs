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
    ILogger _logger;
    HttpClient _httpClient;

    public OutputSender(ILogger<OutputSender> logger, HttpClient httpClient, string uri)
    {
        _logger = logger;
        _httpClient = httpClient;
        Uri = uri;
    }

    public int Order { get; private set; } = 0;

    public string Uri { get; private set; }

    public Task SendAsync(string data, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SendEndAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
