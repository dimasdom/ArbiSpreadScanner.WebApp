using System.Diagnostics;

namespace ArbiScannerWeb.LoadTests.Support;

internal sealed record LoadRunResult(int OkCount, int FailCount);

internal static class LoadRunner
{
    public static int MaxConcurrency(int requestsPerSecond) => Math.Clamp(requestsPerSecond, 4, 200);

    public static async Task<LoadRunResult> RunAsync(Func<Task<bool>> sendRequestAsync, int requestsPerSecond, TimeSpan duration)
    {
        var okCount = 0;
        var failCount = 0;
        var maxConcurrency = MaxConcurrency(requestsPerSecond);
        using var throttle = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        using var cts = new CancellationTokenSource(duration);
        var inFlight = new List<Task>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            while (!cts.IsCancellationRequested)
            {
                var tickStart = stopwatch.Elapsed;

                for (var i = 0; i < requestsPerSecond && !cts.IsCancellationRequested; i++)
                {
                    await throttle.WaitAsync(cts.Token).ConfigureAwait(false);
                    inFlight.Add(SendOneAsync());
                }

                var remaining = TimeSpan.FromSeconds(1) - (stopwatch.Elapsed - tickStart);
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, cts.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        await Task.WhenAll(inFlight).ConfigureAwait(false);

        return new LoadRunResult(okCount, failCount);

        async Task SendOneAsync()
        {
            try
            {
                var ok = await sendRequestAsync().ConfigureAwait(false);
                if (ok)
                {
                    Interlocked.Increment(ref okCount);
                }
                else
                {
                    Interlocked.Increment(ref failCount);
                }
            }
            catch
            {
                Interlocked.Increment(ref failCount);
            }
            finally
            {
                throttle.Release();
            }
        }
    }
}
