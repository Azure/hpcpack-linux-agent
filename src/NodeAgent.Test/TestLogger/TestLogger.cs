using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace NodeAgent.Test.TestLogger;

public class TestLogger : ILogger
{
    private string _category;
    private ITestOutputHelper _outputHelper;

    public TestLogger(string category, ITestOutputHelper outputHelper)
    {
        _category = category;
        _outputHelper = outputHelper;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        throw new NotSupportedException();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            var ts = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var tid = Thread.CurrentThread.ManagedThreadId;
            var msg = $"[{ts}][{_category}][{logLevel}][{tid,2}]: {formatter(state, exception)}";
            _outputHelper.WriteLine(msg);

            if (exception != null)
            {
                _outputHelper.WriteLine(exception.ToString());
            }
        }
    }
}
