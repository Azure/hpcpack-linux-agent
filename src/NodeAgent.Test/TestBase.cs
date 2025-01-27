using Microsoft.Extensions.Logging;
using NodeAgent.Test.TestLogger;
using Xunit.Abstractions;

namespace NodeAgent.Test;

public abstract class TestBase : IDisposable
{
    protected ITestOutputHelper TestOut { get; set; }

    protected ILoggerFactory LoggerFactory { get; set; }

    protected TestBase(ITestOutputHelper output)
    {
        TestOut = output;
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.AddFilter(_ => true);
            builder.AddTestLogger(output);
        });
    }

    public void Dispose()
    {
        LoggerFactory.Dispose();
    }
}
