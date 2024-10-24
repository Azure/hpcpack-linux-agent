namespace NodeAgent.Services;

public interface INamingClient
{
    string GetServiceLocation(string serviceName, CancellationToken cancellationToken);

    void InvalidateCache();
}

public class NamingClient : INamingClient
{
    public string GetServiceLocation(string serviceName, CancellationToken cancellationToken)
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
    public static string ResolveUri(this INamingClient client, string uri, string serviceName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
