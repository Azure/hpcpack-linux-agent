namespace NodeAgent.Services;

//All methods of the interface are thread-safe.
public interface INamingClient
{
    Task<string> GetServiceLocationAsync(string serviceName, CancellationToken cancellationToken);

    void InvalidateCache();
}

public class NamingClient : INamingClient
{
    public Task<string> GetServiceLocationAsync(string serviceName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public void InvalidateCache()
    {
        throw new NotImplementedException();
    }
}

public static class INamingClientExtensions
{
    public static Task<string> ResolveUriAsync(this INamingClient client, string uri, string serviceName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
