namespace NodeAgent.Services;


public interface IOutputSenderFactory
{
    IOutputSender Create(string uri);
}

public class OutputSenderFactory : IOutputSenderFactory
{
    private ILoggerFactory _loggerFactory;
    private IHttpClientFactory _httpClientFactory;

    public OutputSenderFactory(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
    }

    public IOutputSender Create(string uri)
    {
        var logger = _loggerFactory.CreateLogger<OutputSender>();
        var httpClient = _httpClientFactory.CreateClient();
        return new OutputSender(logger, httpClient, uri);
    }
}
