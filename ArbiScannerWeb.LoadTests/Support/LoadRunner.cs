namespace ArbiScannerWeb.LoadTests.Support;

internal sealed record LoadRunResult(int OkCount, int FailCount);

internal static class LoadRunner
{
    public static int MaxConcurrency(int queriesPerMinute) => Math.Clamp((int)Math.Ceiling(queriesPerMinute / 60.0), 4, 200);

    public static async Task<LoadRunResult> RunAsync(Func<Task<bool>> sendRequestAsync, int queriesPerMinute, TimeSpan duration)
    {
        var okCount = 0;
        var failCount = 0;
        var maxConcurrency = MaxConcurrency(queriesPerMinute);
        using var throttle = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        using var cts = new CancellationTokenSource(duration);
        var inFlight = new List<Task>();
        var interval = TimeSpan.FromSeconds(60.0 / queriesPerMinute);

        try
        {
            while (!cts.IsCancellationRequested)
            {
                await throttle.WaitAsync(cts.Token).ConfigureAwait(false);
                inFlight.Add(SendOneAsync());
                await Task.Delay(interval, cts.Token).ConfigureAwait(false);
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
