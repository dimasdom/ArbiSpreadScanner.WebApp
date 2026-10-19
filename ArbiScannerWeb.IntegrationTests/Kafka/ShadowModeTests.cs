using ArbiScannerWeb.Abstractions.Messaging;
using ArbiScannerWeb.Infrastructure.Messaging;
using ArbiScannerWeb.IntegrationTests.Support;
using ArbiScannerWeb.Messaging.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Testcontainers.Redis;
using static ArbiScannerWeb.IntegrationTests.Kafka.KafkaTestSupport;
using ArbiScannerWeb.Domain.Models;

namespace ArbiScannerWeb.IntegrationTests.Kafka;

[Collection(KafkaCollection.Name)]
public sealed class ShadowModeTests(KafkaTestFixture kafka) : IAsyncLifetime
{
    private static readonly DateTimeOffset Minute = new(2026, 5, 1, 10, 5, 0, TimeSpan.Zero);

    private readonly RedisContainer _redis = new RedisBuilder(Images.Redis).Build();
    private ConnectionMultiplexer _connection = default!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private async Task<IHost> StartShadowHostAsync(string topic)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Messaging:ServiceName"] = "webapp",
            ["Messaging:Consumer:Mode"] = "Shadow",
            ["Kafka:BootstrapServers"] = kafka.BootstrapServers,
            ["Kafka:Consumer:Topic"] = topic,
            ["Kafka:Consumer:AutoOffsetReset"] = "Earliest",
            ["Kafka:Consumer:CommitIntervalSeconds"] = "1",
            ["Kafka:Consumer:PollTimeoutMs"] = "200",
            ["Kafka:Consumer:RetryBaseDelayMs"] = "20",
            ["Kafka:Consumer:RetryMaxDelayMs"] = "50",
            ["Kafka:Consumer:MaxHandlerAttempts"] = "2",
        });
        builder.Services.AddSingleton<IConnectionMultiplexer>(_connection);
        builder.Services.AddSpreadMessaging(builder.Configuration);

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private static TradeOpportunityModel Spread() => new()
    {
        Guid = Guid.NewGuid(),
        Symbol = "ETH/USDT",
        Type = SpreadType.Spot,
        ActionType = MarketPositionAction.Open,
        ExchangeRateA = new ExchangeRateModel { Symbol = "ETH/USDT", Exchange = "Kraken", ExchangeRate = 3_000 },
        ExchangeRateB = new ExchangeRateModel { Symbol = "ETH/USDT", Exchange = "Binance", ExchangeRate = 3_050 },
        ExchangeLong = new ExchangeRateModel { Symbol = "ETH/USDT", Exchange = "Kraken", ExchangeRate = 3_000 },
        ExchangeShort = new ExchangeRateModel { Symbol = "ETH/USDT", Exchange = "Binance", ExchangeRate = 3_050 },
        Spread = 1.6,
        StartSpread = 1.6,
    };

    private async Task<long> KafkaSideCountAsync() =>
        await _connection.GetDatabase().SetLengthAsync(RedisParityRecorder.BucketKey("webapp", MessagingBackends.Kafka, Minute));

    [Fact]
    public async Task Kafka_messages_are_recorded_and_compared_against_rabbitmq_with_no_side_effects()
    {
        var topic = await kafka.CreateTopicAsync(partitions: 3);
        using var producer = CreateProducer(kafka.BootstrapServers);
        var spreads = Enumerable.Range(0, 3).Select(_ => Spread()).ToList();
        var produced = 0;
        foreach (var spread in spreads)
        {
            await producer.PublishAsync(topic, SpreadEventKeys.For(spread), TradeOpportunitySerializer.Serialize(spread), "TradeOpportunityModel", $"id-{produced++}", Minute.AddSeconds(5));
        }

        using var host = await StartShadowHostAsync(topic);
        await UntilAsync(() => KafkaSideCountAsync().GetAwaiter().GetResult() == 3, "shadow consumer recorded all three messages");

        // RabbitMQ delivered only two of them.
        var recorder = host.Services.GetRequiredService<IParityRecorder>();
        await recorder.RecordAsync(MessagingBackends.RabbitMq, SpreadEventKeys.For(spreads[0]), "id-0", Minute.AddSeconds(5));
        await recorder.RecordAsync(MessagingBackends.RabbitMq, SpreadEventKeys.For(spreads[1]), "id-1", Minute.AddSeconds(5));
        var comparer = host.Services.GetServices<IHostedService>().OfType<ParityComparerService>().Single();

        var mismatch = await comparer.CompareMinuteAsync(Minute);
        mismatch!.Matched.Should().Be(2);
        mismatch.MissingInRabbitMq.Should().Equal(SpreadEventKeys.For(spreads[2]));
        mismatch.MissingInKafka.Should().BeEmpty();

        await recorder.RecordAsync(MessagingBackends.RabbitMq, SpreadEventKeys.For(spreads[2]), "id-2", Minute.AddSeconds(5));
        (await comparer.CompareMinuteAsync(Minute))!.IsMatch.Should().BeTrue();
    }

    [Fact]
    public async Task A_poison_message_in_the_shadow_stream_is_dead_lettered_and_does_not_block_later_messages()
    {
        var topic = await kafka.CreateTopicAsync(partitions: 1);
        using var producer = CreateProducer(kafka.BootstrapServers);
        var good = Spread();
        await producer.PublishAsync(topic, "k", [0xFF, 0xFF, 0xFF, 0xFF, 0xFF], "TradeOpportunityModel", "id-poison", Minute);
        await producer.PublishAsync(topic, SpreadEventKeys.For(good), TradeOpportunitySerializer.Serialize(good), "TradeOpportunityModel", "id-good", Minute);

        using var host = await StartShadowHostAsync(topic);
        await UntilAsync(() => KafkaSideCountAsync().GetAwaiter().GetResult() == 1, "the valid message after the poison one was recorded");

        ReadAll(kafka.BootstrapServers, topic + ".dlq", expected: 1).Should().ContainSingle();
    }

    [Fact]
    public async Task The_shadow_consumer_uses_its_own_group_so_it_can_never_steal_offsets_from_the_live_group()
    {
        var topic = await kafka.CreateTopicAsync(partitions: 1);
        using var host = await StartShadowHostAsync(topic);

        host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<KafkaConsumerOptions>>().Value.GroupId
            .Should().Be("webapp-spreads-shadow");
    }
}
