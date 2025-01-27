using NodeAgent.Models;
using NodeAgent.Services;
using NodeAgent.Test.Mocks;
using System.Net;
using System.Net.Http.Json;

namespace NodeAgent.Test.Servcies;

public class OutputSenderTest
{
    [Fact]
    public async Task Test()
    {
        var uri = "https://host/path";
        var hostname = "host1";
        var responses = new HttpResponseMessage[] {
            new HttpResponseMessage(HttpStatusCode.NoContent),
            new HttpResponseMessage(HttpStatusCode.NoContent),
            new HttpResponseMessage(HttpStatusCode.NoContent),
            new HttpResponseMessage(HttpStatusCode.NoContent),
        };
        var handler = new MockHttpHandler(responses);
        var httpClient = new HttpClient(handler);
        var sender = new OutputSender(null, httpClient, uri, hostname);
        var messages = new string[]
        {
            "abc",
            "123",
            ""
        };

        foreach (var msg in messages)
        {
            await sender.SendAsync(msg);
            Assert.False(sender.IsEnd);
        }
        await sender.SendEndAsync();
        Assert.True(sender.IsEnd);
        Assert.Equal(messages.Length + 1, handler.Counter);
        for (var i = 0; i < messages.Length; i++)
        {
            var req = handler.Requests[i];
            Assert.NotNull(req.Content);

            var data = await req.Content.ReadFromJsonAsync<OutputData>();
            Assert.NotNull(data);
            Assert.Equal(messages[i], data.Content);
            Assert.Equal(i, data.Order);
            Assert.Equal(hostname, data.NodeName);
            Assert.False(data.Eof);
        }

        var lastReq = handler.Requests.Last();
        Assert.NotNull(lastReq.Content);

        var lastData = await lastReq.Content.ReadFromJsonAsync<OutputData>();
        Assert.NotNull(lastData);
        Assert.Equal(string.Empty, lastData.Content);
        Assert.Equal(messages.Length, lastData.Order);
        Assert.Equal(hostname, lastData.NodeName);
        Assert.True(lastData.Eof);
    }

    [Fact]
    public async Task Test2()
    {
        var uri = "https://host/path";
        var hostname = "host1";
        var responses = new HttpResponseMessage[] {
            new HttpResponseMessage(HttpStatusCode.NoContent),
            new HttpResponseMessage(HttpStatusCode.NoContent),
        };
        var handler = new MockHttpHandler(responses);
        var httpClient = new HttpClient(handler);
        var sender = new OutputSender(null, httpClient, uri, hostname);

        await sender.SendEndAsync();
        Assert.True(sender.IsEnd);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await sender.SendAsync("msg");
        });
    }
}
