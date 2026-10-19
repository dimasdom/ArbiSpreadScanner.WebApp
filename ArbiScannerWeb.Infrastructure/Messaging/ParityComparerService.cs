using System.Diagnostics;
using ArbiScannerWeb.Abstractions.Messaging;
using ArbiScannerWeb.Infrastructure.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ArbiScannerWeb.Infrastructure.Messaging;

public sealed record ParityResult(
    DateTimeOffset Minute,
    int RabbitMqCount,
    int KafkaCount,
    IReadOnlyList<string> MissingInKafka,
    IReadOnlyList<string> MissingInRabbitMq)
{
    public int Matched => RabbitMqCount - MissingInKafka.Count;

    public bool IsMatch => MissingInKafka.Count == 0 && MissingInRabbitMq.Count == 0;
}

/// <summary>
/// Compares, for every closed minute, the set of messages RabbitMQ delivered against the set Kafka delivered, and exports the
/// differences as counters. Only minutes that start after this service started are compared, so the ramp-up of a consumer or
/// a publisher flag flip is not reported as loss.
/// </summary>
public sealed class ParityComparerService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);
    private const int MaxLoggedMembers = 20;

    private readonly IConnectionMultiplexer _redis;
    private readonly MessagingOptions _options;
    private readonly ILogger<ParityComparerService> _logger;
    private readonly TimeProvider _clock;
    private DateTimeOffset _nextMinute;

    public ParityComparerService(
        IConnectionMultiplexer redis,
        IOptions<MessagingOptions> options,
        ILogger<ParityComparerService> logger,
        TimeProvider? clock = null)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
        _nextMinute = FloorToMinute(_clock.GetUtcNow()).AddMinutes(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, _clock);
        do
        {
            try
            {
                await CompareClosedMinutesAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Parity comparison failed; will retry");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Compares every minute that is now closed (older than the grace period) and not yet compared.</summary>
    public async Task<IReadOnlyList<ParityResult>> CompareClosedMinutesAsync()
    {
        var closedBefore = FloorToMinute(_clock.GetUtcNow() - Grace);
        var results = new List<ParityResult>();

        while (_nextMinute <= closedBefore)
        {
            var result = await CompareMinuteAsync(_nextMinute);
            _nextMinute = _nextMinute.AddMinutes(1);

            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    /// <returns><c>null</c> when neither backend delivered anything in that minute.</returns>
    public async Task<ParityResult?> CompareMinuteAsync(DateTimeOffset minute)
    {
        var db = _redis.GetDatabase();
        var rabbit = await ReadIdsAsync(db, MessagingBackends.RabbitMq, minute);
        var kafka = await ReadIdsAsync(db, MessagingBackends.Kafka, minute);

        if (rabbit.Count == 0 && kafka.Count == 0)
        {
            return null;
        }

        var missingInKafka = rabbit.Keys.Where(id => !kafka.ContainsKey(id)).Select(id => rabbit[id]).ToList();
        var missingInRabbit = kafka.Keys.Where(id => !rabbit.ContainsKey(id)).Select(id => kafka[id]).ToList();
        var result = new ParityResult(minute, rabbit.Count, kafka.Count, missingInKafka, missingInRabbit);

        var tags = new TagList { { "service", _options.ServiceName } };
        ParityMetrics.ComparedMinutes.Add(1, tags);
        ParityMetrics.Matched.Add(result.Matched, tags);
        ParityMetrics.MissingInKafka.Add(missingInKafka.Count, tags);
        ParityMetrics.MissingInRabbitMq.Add(missingInRabbit.Count, tags);

        if (result.IsMatch)
        {
            _logger.LogInformation("Parity OK for {Minute:u}: {Count} message(s) on both brokers", minute, rabbit.Count);
        }
        else
        {
            _logger.LogWarning(
                "Parity MISMATCH for {Minute:u}: rabbitmq={RabbitMq} kafka={Kafka} missingInKafka={MissingInKafka} missingInRabbitMq={MissingInRabbitMq}. Sample keys missing in Kafka: {KafkaSample}; missing in RabbitMQ: {RabbitSample}",
                minute,
                rabbit.Count,
                kafka.Count,
                missingInKafka.Count,
                missingInRabbit.Count,
                string.Join(", ", missingInKafka.Distinct().Take(MaxLoggedMembers)),
                string.Join(", ", missingInRabbit.Distinct().Take(MaxLoggedMembers)));
        }

        return result;
    }

    /// <returns>messageId -> spread key.</returns>
    private async Task<Dictionary<string, string>> ReadIdsAsync(IDatabase db, string backend, DateTimeOffset minute)
    {
        var members = await db.SetMembersAsync(RedisParityRecorder.BucketKey(_options.ServiceName, backend, minute));
        var ids = new Dictionary<string, string>(members.Length, StringComparer.Ordinal);

        foreach (var member in members)
        {
            var text = member.ToString();
            var split = text.IndexOf(RedisParityRecorder.MemberSeparator, StringComparison.Ordinal);
            if (split > 0)
            {
                ids[text[..split]] = text[(split + 1)..];
            }
        }

        return ids;
    }

    private static DateTimeOffset FloorToMinute(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, TimeSpan.Zero);
}
