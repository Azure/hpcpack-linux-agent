using Microsoft.Extensions.Options;

namespace NodeAgent.Utils;

public static class LoopWork
{
    public class StartOptions
    {
        public int HoldSeconds { get; set; }

        public int IntervalSeconds { get; set; }

        public int ErrorRetryMultiplyFactor { get; set; }

        //TODO: Make it for logging purpose
        public override string? ToString()
        {
            return base.ToString();
        }
    }

    public static async Task StartAsync(Func<CancellationToken, Task<bool>> work, Func<int, CancellationToken, Task> onError,
        CancellationToken stoppingToken, IOptionsMonitor<StartOptions> options)
    {
        int retryCount = 0;
        await Task.Delay(options.CurrentValue.HoldSeconds * 1000, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!await work(stoppingToken).ConfigureAwait(false))
            {
                await onError(retryCount++, stoppingToken).ConfigureAwait(false);
            }
            else
            {
                retryCount = 0;
            }

            int sleepSeconds = 0;
            if (retryCount > 0)
            {
                //NOTE: Caution the overflow for int type of retrySeconds
                int retrySeconds = 2 * (int)Math.Pow(options.CurrentValue.ErrorRetryMultiplyFactor, retryCount);
                sleepSeconds = retrySeconds > 0 && retrySeconds < options.CurrentValue.IntervalSeconds ?
                    retrySeconds : options.CurrentValue.IntervalSeconds;
            }
            else
            {
                sleepSeconds = options.CurrentValue.IntervalSeconds;
            }
            await Task.Delay(sleepSeconds * 1000, stoppingToken).ConfigureAwait(false);
        }
    }
}
