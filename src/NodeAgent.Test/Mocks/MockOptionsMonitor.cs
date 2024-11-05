using Microsoft.Extensions.Options;

namespace NodeAgent.Test.Mocks;

public class MockOptionsMonitor<T> : IOptionsMonitor<T> where T : class
{
    private T _value;

    public MockOptionsMonitor(T value)
    {
        _value = value;
    }

    public T CurrentValue => _value;

    public T Get(string? name)
    {
        throw new NotSupportedException();
    }

    public IDisposable? OnChange(Action<T, string?> listener)
    {
        throw new NotSupportedException();
    }
}
