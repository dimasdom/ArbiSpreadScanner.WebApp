using StackExchange.Redis;

namespace ArbiScannerWeb.Messaging.Kafka;

public enum ClaimResult
{
    /// <summary>This caller now owns the message and must call Complete or Abandon.</summary>
    Started,

    /// <summary>Another worker (or an earlier crashed attempt) holds a still-live claim.</summary>
    InProgress,

    /// <summary>The message was fully processed before; skip it.</summary>
    AlreadyDone,
}

/// <summary>Two-state duplicate suppression: a short-lived <c>processing</c> claim, upgraded to a long-lived
/// <c>done</c> marker only after the side effects succeeded. A crash mid-handler leaves only the short claim,
/// so the redelivered message is retried once it expires instead of being skipped as a "duplicate" and lost.</summary>
public interface IIdempotencyStore
{
    Task<ClaimResult> TryBeginAsync(string key, CancellationToken cancellationToken);

    Task CompleteAsync(string key);

    Task AbandonAsync(string key);
}

public sealed class RedisIdempotencyStore(IConnectionMultiplexer redis) : IIdempotencyStore
{
    public static readonly TimeSpan ProcessingTtl = TimeSpan.FromSeconds(60);

    public static readonly TimeSpan DoneTtl = TimeSpan.FromMinutes(15);

    private const string Processing = "processing";
    private const string Done = "done";

    public async Task<ClaimResult> TryBeginAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = redis.GetDatabase();

        if (await db.StringSetAsync(key, Processing, ProcessingTtl, When.NotExists))
        {
            return ClaimResult.Started;
        }

        var state = await db.StringGetAsync(key);
        if (state.IsNull)
        {
            // Expired between the SET and the GET — try to claim again.
            return await db.StringSetAsync(key, Processing, ProcessingTtl, When.NotExists)
                ? ClaimResult.Started
                : ClaimResult.InProgress;
        }

        return state == Done ? ClaimResult.AlreadyDone : ClaimResult.InProgress;
    }

    public Task CompleteAsync(string key) => redis.GetDatabase().StringSetAsync(key, Done, DoneTtl);

    public Task AbandonAsync(string key) => redis.GetDatabase().KeyDeleteAsync(key);
}

/// <summary>Runs a side-effecting action at most once per idempotency key.</summary>
public static class IdempotentExecutor
{
    /// <returns><c>true</c> if <paramref name="work"/> ran, <c>false</c> if the key was already done.</returns>
    public static async Task<bool> RunOnceAsync(
        IIdempotencyStore store,
        string key,
        Func<CancellationToken, Task> work,
        TimeSpan maxWait,
        TimeSpan pollInterval,
        CancellationToken cancellationToken,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(work);

        var time = clock ?? TimeProvider.System;
        var deadline = time.GetUtcNow() + maxWait;

        while (true)
        {
            switch (await store.TryBeginAsync(key, cancellationToken))
            {
                case ClaimResult.AlreadyDone:
                    return false;

                case ClaimResult.Started:
                    try
                    {
                        await work(cancellationToken);
                    }
                    catch
                    {
                        await store.AbandonAsync(key);
                        throw;
                    }

                    await store.CompleteAsync(key);
                    return true;

                default:
                    if (time.GetUtcNow() >= deadline)
                    {
                        throw new TimeoutException($"Idempotency claim for '{key}' stayed in progress for {maxWait}.");
                    }

                    await Task.Delay(pollInterval, time, cancellationToken);
                    break;
            }
        }
    }
}
