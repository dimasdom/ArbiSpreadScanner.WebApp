using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.IntegrationTests.Fixtures;
using ArbiScannerWeb.IntegrationTests.Support;
using ArbiScannerWeb.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using RabbitMQ.Client;

namespace ArbiScannerWeb.IntegrationTests.RabbitMq;

[Collection(RabbitMqCollection.Name)]
public class RabbitMqIdempotencyTests(RabbitMqTestFixture fixture)
{
    [Fact]
    public async Task DuplicateDelivery_OfSameEvent_ResultsInExactlyOneTickerWrite()
    {
        var message = BuildSpread();

        // The same event, delivered twice — simulates an at-least-once redelivery
        // (broker restart, missed ack, etc.) rather than two distinct spreads.
        await PublishAsync(message);
        await PublishAsync(message);

        var tickers = fixture.Factory.Services.GetRequiredService<IMongoDatabase>()
            .GetCollection<TradeOpportunityTickerModel>("SpreadsTicker");

        var deadline = DateTime.UtcNow.AddSeconds(20);
        long count = 0;
        while (DateTime.UtcNow < deadline)
        {
            count = await tickers.CountDocumentsAsync(Builders<TradeOpportunityTickerModel>.Filter.Eq(x => x.Guid, message.Guid));
            if (count >= 1)
                break;

            await Task.Delay(500);
        }

        // Give a second delivery, if it were going to be (incorrectly) processed, time to land.
        await Task.Delay(3000);
        count = await tickers.CountDocumentsAsync(Builders<TradeOpportunityTickerModel>.Filter.Eq(x => x.Guid, message.Guid));

        count.Should().Be(1, "the Redis dedupe key should have skipped the second, duplicate delivery");
    }

    [Fact]
    public async Task DistinctUpdates_ForSamePosition_AreAllProcessed()
    {
        var open = BuildSpread();
        await PublishAsync(open);

        // Same Guid and ActionType as every other Update for this position — only the
        // spread/rate differ. These must NOT collide with the dedupe key and get skipped
        // as if they were a redelivery of the previous tick.
        await PublishAsync(BuildSpread(MarketPositionAction.Update, open.Guid, spread: 2.50));
        await PublishAsync(BuildSpread(MarketPositionAction.Update, open.Guid, spread: 2.75));

        var tickers = fixture.Factory.Services.GetRequiredService<IMongoDatabase>()
            .GetCollection<TradeOpportunityTickerModel>("SpreadsTicker");

        var deadline = DateTime.UtcNow.AddSeconds(20);
        long count = 0;
        while (DateTime.UtcNow < deadline)
        {
            count = await tickers.CountDocumentsAsync(Builders<TradeOpportunityTickerModel>.Filter.Eq(x => x.Guid, open.Guid));
            if (count >= 3)
                break;

            await Task.Delay(500);
        }

        count.Should().Be(3, "each distinct spread/rate update for the same position is new data, not a redelivery, and must not be skipped by the dedupe key");
    }

    private async Task PublishAsync(TradeOpportunityModel message)
    {
        var factory = new ConnectionFactory
        {
            HostName = fixture.BrokerEndpoint.Host,
            Port = fixture.BrokerEndpoint.Port,
            UserName = fixture.BrokerEndpoint.Username,
            Password = fixture.BrokerEndpoint.Password
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var dlxArgs = new Dictionary<string, object?> { ["x-dead-letter-exchange"] = RabbitMqService.DeadLetterExchange };

        await channel.ExchangeDeclareAsync(RabbitMqService.DeadLetterExchange, ExchangeType.Fanout, durable: true);
        await channel.ExchangeDeclareAsync(RabbitMqTestFixture.Exchange, ExchangeType.Fanout, durable: true);
        await channel.QueueDeclareAsync(RabbitMqTestFixture.Queue, durable: true, exclusive: false, autoDelete: false, arguments: dlxArgs);
        await channel.QueueBindAsync(RabbitMqTestFixture.Queue, RabbitMqTestFixture.Exchange, routingKey: "");

        var body = ProtobufTestSerializer.Serialize(message);
        await channel.BasicPublishAsync(RabbitMqTestFixture.Exchange, routingKey: "", body: body);
    }

    private static TradeOpportunityModel BuildSpread(MarketPositionAction actionType = MarketPositionAction.Open, Guid? guid = null, double spread = 2.25)
    {
        ExchangeRateModel Rate(string exchange) => new()
        {
            Symbol = "BTC/USDT",
            Exchange = exchange,
            ExchangeRate = 50_000,
            VolumeAsk = 100,
            VolumeBid = 100
        };

        return new TradeOpportunityModel
        {
            Guid = guid ?? Guid.NewGuid(),
            Symbol = "BTC/USDT",
            Type = SpreadType.Spot,
            ActionType = actionType,
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
}
