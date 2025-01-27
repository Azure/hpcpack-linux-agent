using NodeAgent.Services;
using NodeAgent.Test.Mocks;
using System.Net.Http.Json;
using System.Net;

namespace NodeAgent.Test.Servcies;

public class NamingClientTest
{
    [Theory]
    [InlineData("https://head1/path")]
    [InlineData("https://head1/path", "https://head2/path")]
    public async Task TestGetServiceLocationOk(params string[] namingServiceUris)
    {
        var configManager = new MockConfigManager();
        configManager.Config.NamingServiceUri = namingServiceUris;
        var location = "location";
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(location) };
        var httpClientFactory = MockHttpClientFactory.Create(response);
        var namingClient = new NamingClient(null, configManager, httpClientFactory);

        //Test thread-safety by concurrent calls
        var tasks = new Task<string>[3];
        for (var i = 0; i < 3; i++)
        {
            tasks[i] = namingClient.GetServiceLocationAsync("service");
        }
        await Task.WhenAll(tasks);
        for (var i = 0; i < 3; i++)
        {
#pragma warning disable xUnit1031
            Assert.Equal(location, tasks[i].Result);
#pragma warning restore xUnit1031
        }
    }

    [Theory]
    [InlineData("https://head1/path")]
    [InlineData("https://head1/path", "https://head2/path")]
    public async Task TestGetServiceLocationOk2(params string[] namingServiceUris)
    {
        var configManager = new MockConfigManager();
        configManager.Config.NamingServiceUri = namingServiceUris;
        var location = "location";
        var responses = new HttpResponseMessage[] {
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(location) }
        };
        var httpClientFactory = MockHttpClientFactory.Create(responses);
        var namingClient = new NamingClient(null, configManager, httpClientFactory);

        //Test thread-safety by concurrent calls
        var tasks = new Task<string>[3];
        for (var i = 0; i < 3; i++)
        {
            tasks[i] = namingClient.GetServiceLocationAsync("service");
        }
        await Task.WhenAll(tasks);
        for (var i = 0; i < 3; i++)
        {
#pragma warning disable xUnit1031
            Assert.Equal(location, tasks[i].Result);
#pragma warning restore xUnit1031
        }
    }

    [Theory]
    [InlineData("https://head1/path")]
    [InlineData("https://head1/path", "https://head2/path")]
    public async Task TestGetServiceLocationCancel(params string[] namingServiceUris)
    {
        var configManager = new MockConfigManager();
        configManager.Config.NamingServiceUri = namingServiceUris;
        var location = "location";
        var responses = new HttpResponseMessage[] {
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(location) }
        };
        var httpClientFactory = MockHttpClientFactory.Create(responses);
        var namingClient = new NamingClient(null, configManager, httpClientFactory);

        //Test thread-safety by concurrent calls
        var cts = new CancellationTokenSource();
        var tasks = new Task<string>[3];
        for (var i = 0; i < 3; i++)
        {
            tasks[i] = namingClient.GetServiceLocationAsync("service", cts.Token);
        }
        cts.Cancel();

        await Assert.ThrowsAsync<AggregateException>(async () =>
        {
            await Task.WhenAll(tasks);
        });

        for (var i = 0; i < 3; i++)
        {
            var exception = tasks[i].Exception!.Flatten().InnerExceptions.FirstOrDefault(ex =>
            {
                return ex is OperationCanceledException;
            });
            Assert.NotNull(exception);
        }
    }

    [Theory]
    [InlineData("https://head1/path")]
    [InlineData("https://head1/path", "https://head2/path")]
    public async Task TestResolveUri(params string[] namingServiceUris)
    {
        var configManager = new MockConfigManager();
        configManager.Config.NamingServiceUri = namingServiceUris;
        var location = "location";
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(location) };
        var httpClientFactory = MockHttpClientFactory.Create(response);
        var namingClient = new NamingClient(null, configManager, httpClientFactory);

        var uri1 = "https://host/path";
        var result1 = await namingClient.ResolveUriAsync(uri1, "service");
        Assert.Equal(uri1, result1);

        var uri2 = "https://{0}/path";
        var expected2 = string.Format(uri2, location);
        var result2 = await namingClient.ResolveUriAsync(uri2, "service");
        Assert.Equal(expected2, result2);
    }
}
