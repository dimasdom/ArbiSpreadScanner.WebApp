using System.Text;
using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Messaging;
using ArbiScannerWeb.IntegrationTests.Fixtures;
using ArbiScannerWeb.Messaging.Kafka;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using static ArbiScannerWeb.IntegrationTests.Kafka.KafkaTestSupport;

namespace ArbiScannerWeb.IntegrationTests.Kafka;

/// <summary>The cutover end to end: spread events produced to Kafka reach the real web API's consumer group and change real
/// Mongo state, with RabbitMQ nowhere in the picture.</summary>
[Collection(KafkaWebCollection.Name)]
public sealed class KafkaCutoverTests(KafkaWebTestFixture fixture)
{
    private IMongoCollection<TradeOpportunityTickerModel> Tickers =>
        fixture.Factory.Services.GetRequiredService<IMongoDatabase>().GetCollection<TradeOpportunityTickerModel>("SpreadsTicker");

    private IMongoCollection<TradeOpportunityModel> Spreads =>
        fixture.Factory.Services.GetRequiredService<IMongoDatabase>().GetCollection<TradeOpportunityModel>("CurrentSpreads");

    private static TradeOpportunityModel Build(MarketPositionAction action, Guid guid, string symbol, double spread)
    {
        ExchangeRateModel Rate(string exchange) => new() { Symbol = symbol, Exchange = exchange, ExchangeRate = 50_000, VolumeAsk = 100, VolumeBid = 100 };

        return new TradeOpportunityModel
        {
            Guid = guid,
            Symbol = symbol,
            Type = SpreadType.Spot,
            ActionType = action,
            ExchangeRateA = Rate("Binance"),
            ExchangeRateB = Rate("Bybit"),
            ExchangeShort = Rate("Binance"),
            ExchangeLong = Rate("Bybit"),
            Spread = spread,
            StartSpread = 2.25,
            SummaryTarrif = 0.1,
            PossibleProfit = 15,
        };
    }

    private static Task<ProducedMessage> Send(KafkaMessageProducer producer, TradeOpportunityModel model, string? messageId = null) =>
        producer.PublishAsync(
            KafkaWebTestFixture.Topic,
            SpreadEventKeys.For(model),
            TradeOpportunitySerializer.Serialize(model),
            "TradeOpportunityModel",
            messageId ?? Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow);

    private async Task<long> TickerCount(Guid guid) => await Tickers.CountDocumentsAsync(Builders<TradeOpportunityTickerModel>.Filter.Eq(x => x.Guid, guid));

    [Fact]
    public async Task A_spread_lifecycle_produced_to_kafka_is_applied_in_order_to_mongo()
    {
        using var producer = CreateProducer(fixture.BootstrapServers);
        var guid = Guid.NewGuid();
        const string symbol = "LIFE/USDT";

        await Send(producer, Build(MarketPositionAction.Open, guid, symbol, 2.25));
        await UntilAsync(() => Spreads.CountDocuments(Builders<TradeOpportunityModel>.Filter.Eq(x => x.Guid, guid)) == 1, "Open created the spread");
        await UntilAsync(() => TickerCount(guid).GetAwaiter().GetResult() == 1, "Open wrote the first ticker");

        await Send(producer, Build(MarketPositionAction.Update, guid, symbol, 2.50));
        await Send(producer, Build(MarketPositionAction.Update, guid, symbol, 2.75));
        await UntilAsync(() => TickerCount(guid).GetAwaiter().GetResult() == 3, "two Updates added two tickers");

        var current = await Spreads.Find(Builders<TradeOpportunityModel>.Filter.Eq(x => x.Guid, guid)).SingleAsync();
        current.Spread.Should().Be(2.75, "Updates are applied in produce order, so the last one wins");

        await Send(producer, Build(MarketPositionAction.Close, guid, symbol, 0));
        await UntilAsync(() => TickerCount(guid).GetAwaiter().GetResult() == 0, "Close removed the tickers");
        var closed = await Spreads.Find(Builders<TradeOpportunityModel>.Filter.Eq(x => x.Guid, guid)).SingleAsync();
        closed.OrderStatus.Should().Be(OrderStatus.Closed);
    }

    [Fact]
    public async Task A_duplicate_delivery_writes_no_duplicate_ticker()
    {
        using var producer = CreateProducer(fixture.BootstrapServers);
        var guid = Guid.NewGuid();
        var open = Build(MarketPositionAction.Open, guid, "DUPE/USDT", 2.25);

        await Send(producer, open, messageId: "same-id");
        await Send(producer, open, messageId: "same-id");
        // A later, distinct event proves the duplicate has been consumed past without being applied.
        await Send(producer, Build(MarketPositionAction.Update, guid, "DUPE/USDT", 2.5));

        await UntilAsync(() => TickerCount(guid).GetAwaiter().GetResult() >= 2, "the later update was applied");
        await Task.Delay(1_000);

        (await TickerCount(guid)).Should().Be(2, "one ticker for the Open (not two) and one for the Update");
    }

    [Fact]
    public async Task A_poison_message_is_dead_lettered_and_the_consumer_keeps_working()
    {
        using var producer = CreateProducer(fixture.BootstrapServers);
        var guid = Guid.NewGuid();
        await producer.PublishAsync(KafkaWebTestFixture.Topic, "poison-key", Encoding.UTF8.GetBytes("not protobuf at all"), "TradeOpportunityModel", "poison-1", DateTimeOffset.UtcNow);
        await Send(producer, Build(MarketPositionAction.Open, guid, "AFTER/USDT", 2.25));

        await UntilAsync(() => TickerCount(guid).GetAwaiter().GetResult() == 1, "a valid message after the poison one was applied");

        var dead = ReadAll(fixture.BootstrapServers, KafkaWebTestFixture.Topic + ".dlq", expected: 1)
            .Where(r => r.Message.Key == "poison-key").ToList();
        dead.Should().ContainSingle();
        MessageHeaders.GetString(dead[0].Message.Headers, MessageHeaders.DlqAttempts).Should().Be("1", "an undecodable payload is non-retryable");
    }

    [Fact]
    public async Task The_app_consumes_in_the_live_group_and_exposes_a_kafka_health_check()
    {
        var group = fixture.Factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<KafkaConsumerOptions>>().Value.GroupId;
        group.Should().Be("webapp-spreads");

        var health = fixture.Factory.Services.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();
        var report = await health.CheckHealthAsync();

        report.Entries.Keys.Should().Contain("kafka").And.NotContain("rabbitmq");
        report.Entries["kafka"].Status.Should().Be(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy);
        fixture.Factory.Services.GetService<IRabbitMqService>().Should().BeNull("RabbitMQ is not consumed in Kafka mode");
    }
}
