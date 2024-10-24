using Microsoft.Extensions.Options;

namespace NodeAgent.Utils;

public class ChangableOptions<T> : IOptionsMonitor<T> where T : class
{
    private T _value;

    public ChangableOptions(T value)
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
