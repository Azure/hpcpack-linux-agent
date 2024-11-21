namespace NodeAgent.Test.Mocks;

public class MockHttpHandler : DelegatingHandler
{
    private IList<HttpResponseMessage> _responses = new List<HttpResponseMessage>();
    private IList<HttpRequestMessage> _requests = new List<HttpRequestMessage>();
    private int _current = -1;

    public int Counter => _current + 1;

    public IList<HttpRequestMessage> Requests => _requests;

    public MockHttpHandler(HttpResponseMessage response)
    {
        _responses.Add(response);
    }

    public MockHttpHandler(IEnumerable<HttpResponseMessage> responses)
    {
        foreach (var response in responses)
        {
            _responses.Add(response);
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _current);
        if (_current > _responses.Count - 1)
        {
            throw new InvalidOperationException("Test error!");
        }
        _requests.Add(request);
        return Task.FromResult(_responses[_current]);
    }
}
