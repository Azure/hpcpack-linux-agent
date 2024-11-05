using Microsoft.Extensions.DependencyInjection;

namespace NodeAgent.Test.Mocks;

public static class MockHttpClientFactory
{
    public static IHttpClientFactory Create(HttpResponseMessage response)
    {
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddHttpMessageHandler<MockHttpHandler>();
        });
        services.AddTransient<MockHttpHandler>((_) =>
        {
            return new MockHttpHandler(response);
        });
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>();
    }

    public static IHttpClientFactory Create(IEnumerable<HttpResponseMessage> responses)
    {
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddHttpMessageHandler<MockHttpHandler>();
        });
        services.AddTransient<MockHttpHandler>((_) =>
        {
            return new MockHttpHandler(responses);
        });
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>();
    }
}
