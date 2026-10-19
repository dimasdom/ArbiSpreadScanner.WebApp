using System.Diagnostics.Metrics;
using ArbiScannerWeb.Abstractions.Messaging;
using ArbiScannerWeb.Infrastructure.Messaging;
using ArbiScannerWeb.Infrastructure.Settings;
using ArbiScannerWeb.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace ArbiScannerWeb.IntegrationTests.Kafka;

internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

public sealed class ParityComparerTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 5, 1, 10, 0, 20, TimeSpan.Zero);

    private readonly RedisContainer _redis = new RedisBuilder(Images.Redis).Build();
    private ConnectionMultiplexer _connection = default!;
    private ManualClock _clock = default!;
    private RedisParityRecorder _recorder = default!;
    private ParityComparerService _comparer = default!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        _clock = new ManualClock(Start);
        var options = Options.Create(new MessagingOptions { ServiceName = "webapp" });
        _recorder = new RedisParityRecorder(_connection, options);
        _comparer = new ParityComparerService(_connection, options, NullLogger<ParityComparerService>.Instance, _clock);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private static readonly DateTimeOffset Minute = new(2026, 5, 1, 10, 5, 0, TimeSpan.Zero);

    [Fact]
    public async Task Messages_delivered_by_both_brokers_match()
    {
        foreach (var id in new[] { "a", "b", "c" })
        {
            await _recorder.RecordAsync(MessagingBackends.RabbitMq, "futures:binance:bybit:BTC-USDT", id, Minute.AddSeconds(10));
            await _recorder.RecordAsync(MessagingBackends.Kafka, "futures:binance:bybit:BTC-USDT", id, Minute.AddSeconds(10));
        }

        var result = await _comparer.CompareMinuteAsync(Minute);

        result.Should().NotBeNull();
        result!.IsMatch.Should().BeTrue();
        result.Matched.Should().Be(3);
    }

    [Fact]
    public async Task Redelivery_of_the_same_message_is_not_double_counted()
    {
        await _recorder.RecordAsync(MessagingBackends.RabbitMq, "k", "a", Minute);
        await _recorder.RecordAsync(MessagingBackends.RabbitMq, "k", "a", Minute);
        await _recorder.RecordAsync(MessagingBackends.Kafka, "k", "a", Minute);

        var result = await _comparer.CompareMinuteAsync(Minute);

        result!.RabbitMqCount.Should().Be(1);
        result.IsMatch.Should().BeTrue();
    }

    [Fact]
    public async Task Reports_messages_missing_from_either_side_with_their_keys()
    {
        await _recorder.RecordAsync(MessagingBackends.RabbitMq, "key-1", "only-rabbit", Minute);
        await _recorder.RecordAsync(MessagingBackends.Kafka, "key-2", "only-kafka", Minute);
        await _recorder.RecordAsync(MessagingBackends.RabbitMq, "key-3", "both", Minute);
        await _recorder.RecordAsync(MessagingBackends.Kafka, "key-3", "both", Minute);

        var result = await _comparer.CompareMinuteAsync(Minute);

        result!.IsMatch.Should().BeFalse();
        result.MissingInKafka.Should().Equal("key-1");
        result.MissingInRabbitMq.Should().Equal("key-2");
        result.Matched.Should().Be(1);
    }

    [Fact]
    public async Task A_minute_with_no_traffic_on_either_broker_is_skipped()
    {
        (await _comparer.CompareMinuteAsync(Minute)).Should().BeNull();
    }

    [Fact]
    public async Task Events_are_bucketed_by_the_producer_timestamp_so_a_minute_boundary_does_not_split_a_pair()
    {
        // Both brokers carry the same producer timestamp, so the pair lands in the same minute even if consumed a minute apart.
        await _recorder.RecordAsync(MessagingBackends.RabbitMq, "k", "edge", Minute.AddSeconds(59));
        await _recorder.RecordAsync(MessagingBackends.Kafka, "k", "edge", Minute.AddSeconds(59));

        (await _comparer.CompareMinuteAsync(Minute))!.IsMatch.Should().BeTrue();
        (await _comparer.CompareMinuteAsync(Minute.AddMinutes(1))).Should().BeNull();
    }

    [Fact]
    public async Task Buckets_expire_so_parity_data_cannot_grow_without_bound()
    {
        await _recorder.RecordAsync(MessagingBackends.Kafka, "k", "a", Minute);

        var ttl = await _connection.GetDatabase().KeyTimeToLiveAsync(RedisParityRecorder.BucketKey("webapp", MessagingBackends.Kafka, Minute));

        ttl.Should().BeGreaterThan(TimeSpan.FromHours(2)).And.BeLessThanOrEqualTo(RedisParityRecorder.Retention);
    }

    [Fact]
    public async Task Only_closed_minutes_after_startup_are_compared_and_each_exactly_once()
    {
        // Service "started" at 10:00:20. Its first candidate minute is 10:01 (a full minute after start), and a minute is only
        // compared once it is older than the 2 minute grace period.
        var minute = new DateTimeOffset(2026, 5, 1, 10, 1, 0, TimeSpan.Zero);
        await _recorder.RecordAsync(MessagingBackends.RabbitMq, "k", "a", minute.AddSeconds(5));

        (await _comparer.CompareClosedMinutesAsync()).Should().BeEmpty("10:01 is not yet closed at 10:00:20");

        _clock.Advance(TimeSpan.FromMinutes(3));
        var results = await _comparer.CompareClosedMinutesAsync();
        results.Should().ContainSingle().Which.Minute.Should().Be(minute);
        results[0].MissingInKafka.Should().Equal("k");

        (await _comparer.CompareClosedMinutesAsync()).Should().BeEmpty("a minute is never compared twice");
    }

    [Fact]
    public async Task Differences_are_exported_as_counters_tagged_with_the_service()
    {
        var totals = new Dictionary<string, long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == ParityMetrics.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var service = tags.ToArray().Single(t => t.Key == "service").Value;
            lock (totals)
            {
                totals[$"{instrument.Name}|{service}"] = totals.GetValueOrDefault($"{instrument.Name}|{service}") + value;
            }
        });
        listener.Start();

        await _recorder.RecordAsync(MessagingBackends.RabbitMq, "k", "a", Minute);
        await _recorder.RecordAsync(MessagingBackends.RabbitMq, "k", "b", Minute);
        await _recorder.RecordAsync(MessagingBackends.Kafka, "k", "b", Minute);
        await _comparer.CompareMinuteAsync(Minute);

        lock (totals)
        {
            totals["arbiscanner.messaging.parity.missing_in_kafka|webapp"].Should().Be(1);
            totals["arbiscanner.messaging.parity.matched|webapp"].Should().Be(1);
            totals["arbiscanner.messaging.parity.missing_in_rabbitmq|webapp"].Should().Be(0);
            totals["arbiscanner.messaging.parity.compared_minutes|webapp"].Should().Be(1);
        }
    }
}
