using ArbiScannerWeb.Abstractions.Messaging;
using StackExchange.Redis;

namespace ArbiScannerWeb.Infrastructure.Messaging;

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
