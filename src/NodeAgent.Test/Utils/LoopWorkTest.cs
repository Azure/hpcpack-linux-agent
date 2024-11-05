
using NodeAgent.Test.Mocks;
using NodeAgent.Utils;
using static NodeAgent.Utils.LoopWork;

namespace NodeAgent.Test.Utils;

public class LoopWorkTest
{
    [Theory]
    [InlineData(1, 1, 1500, 1)]
    [InlineData(1, 1, 2500, 2)]
    [InlineData(0, 1, 1500, 2)]
    [InlineData(0, 1, 2500, 3)]
    public async Task TestStart(int HoldSeconds, int IntervalSeconds, int waitMS, int expectedWorkCount)
    {
        var options = new StartOptions()
        {
            HoldSeconds = HoldSeconds,
            IntervalSeconds = IntervalSeconds,
            ErrorRetryMultiplyFactor = 1,
        };
        var optMonitor = new MockOptionsMonitor<StartOptions>(options);
        var cts = new CancellationTokenSource();
        var workCount = 0;
        var work = (CancellationToken token) =>
        {
            workCount++;
            return Task.FromResult(true);
        };
        var errorCount = 0;
        var onError = (int retry, CancellationToken token) =>
        {
            errorCount++;
            return Task.CompletedTask;
        };

        _ = LoopWork.StartAsync(work, onError, cts.Token, optMonitor);
        await Task.Delay(waitMS);
        cts.Cancel();

        Assert.Equal(expectedWorkCount, workCount);
        Assert.Equal(0, errorCount);
    }

    [Theory]
    [InlineData(0, 1, 1, 3, 3500, 4, 3)]
    [InlineData(1, 1, 1, 3, 3500, 3, 3)]
    public async Task TestStartError(int HoldSeconds, int IntervalSeconds, int retryMultiplyFactor, 
        int countBeforeOk, int waitMS, int expectedWorkCount, int expectedErrorCount)
    {
        var options = new StartOptions()
        {
            HoldSeconds = HoldSeconds,
            IntervalSeconds = IntervalSeconds,
            ErrorRetryMultiplyFactor = retryMultiplyFactor,
        };
        var optMonitor = new MockOptionsMonitor<StartOptions>(options);
        var cts = new CancellationTokenSource();
        var workCount = 0;
        var work = (CancellationToken token) =>
        {
            workCount++;
            return Task.FromResult(workCount > countBeforeOk);
        };
        var errorCount = 0;
        var onError = (int retry, CancellationToken token) =>
        {
            Assert.True(workCount <= countBeforeOk);
            Assert.Equal(workCount - 1, retry);
            errorCount++;
            return Task.CompletedTask;
        };

        _ = LoopWork.StartAsync(work, onError, cts.Token, optMonitor);
        await Task.Delay(waitMS);
        cts.Cancel();

        Assert.Equal(expectedWorkCount, workCount);
        Assert.Equal(expectedErrorCount, errorCount);
    }
}
