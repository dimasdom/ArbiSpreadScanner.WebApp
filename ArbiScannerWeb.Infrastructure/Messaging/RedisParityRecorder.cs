using System.Globalization;
using ArbiScannerWeb.Abstractions.Messaging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ArbiScannerWeb.Infrastructure.Settings;

namespace ArbiScannerWeb.Infrastructure.Messaging;

/// <summary>
/// Writes one Redis set per (service, backend, minute) whose members are <c>{messageId}|{key}</c>. Sets (not counters) make the
/// comparison exact and redelivery-proof: the same message recorded twice is still one member. Sets expire after
/// <see cref="Retention"/>; at the current ~100 events/min that is a few hundred small members per bucket.
/// </summary>
public sealed class RedisParityRecorder(IConnectionMultiplexer redis, IOptions<MessagingOptions> options) : IParityRecorder
{
    public static readonly TimeSpan Retention = TimeSpan.FromHours(3);

    public const char MemberSeparator = '|';

    public static string BucketKey(string service, string backend, DateTimeOffset producedAt) =>
        string.Create(CultureInfo.InvariantCulture, $"parity:{service}:{backend}:{producedAt.UtcDateTime:yyyyMMddHHmm}");

    public async Task RecordAsync(string backend, string key, string messageId, DateTimeOffset producedAt)
    {
        var bucket = BucketKey(options.Value.ServiceName, backend, producedAt);
        var db = redis.GetDatabase();

        await db.SetAddAsync(bucket, $"{messageId}{MemberSeparator}{key}");
        await db.KeyExpireAsync(bucket, Retention, ExpireWhen.HasNoExpiry, CommandFlags.FireAndForget);
    }
}
