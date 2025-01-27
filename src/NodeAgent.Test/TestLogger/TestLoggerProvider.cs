using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Xunit.Abstractions;

namespace NodeAgent.Test.TestLogger;

public class TestLoggerProvider : ILoggerProvider
{
    private ITestOutputHelper _outputHelper;
    private ConcurrentDictionary<string, ILogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

    public TestLoggerProvider(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, (name) => new TestLogger(name, _outputHelper));
    }

    public void Dispose()
    {
    }
}

static class ILoggingBuilderExtensions
{
    public static ILoggingBuilder AddTestLogger(this ILoggingBuilder builder, ITestOutputHelper outputHelper)
    {
        builder.Services.AddSingleton<ILoggerProvider, TestLoggerProvider>(_ => new TestLoggerProvider(outputHelper));
        return builder;
    }
}
