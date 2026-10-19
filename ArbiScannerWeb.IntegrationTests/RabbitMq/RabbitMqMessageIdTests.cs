using System.Collections.Concurrent;
using ArbiScannerWeb.Abstractions.Messaging;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Messaging;
using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Infrastructure.Settings;
using ArbiScannerWeb.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace ArbiScannerWeb.IntegrationTests.RabbitMq;

/// <summary>Messages stamped with a message-id by the scanner: two-state dedupe shared with the Kafka consumer,
/// and parity recording. Messages without one keep the legacy content-hash behaviour (RabbitMqIdempotencyTests).</summary>
[Collection(RabbitMqCollection.Name)]
public class RabbitMqMessageIdTests(RabbitMqTestFixture fixture)
{
    private sealed class CapturingParityRecorder : IParityRecorder
    {
        public ConcurrentQueue<(string Backend, string Key, string MessageId, DateTimeOffset ProducedAt)> Calls { get; } = new();

        public Task RecordAsync(string backend, string key, string messageId, DateTimeOffset producedAt)
        {
            Calls.Enqueue((backend, key, messageId, producedAt));
            return Task.CompletedTask;
        }
    }

    private IConnectionMultiplexer Redis => fixture.Factory.Services.GetRequiredService<IConnectionMultiplexer>();

    private RabbitMqService CreateService(string queue, IParityRecorder parity, string serviceName) => new(
        NullLogger<RabbitMqService>.Instance,
        Options.Create(new RabbitMqSettings
        {
            Host = fixture.BrokerEndpoint.Host,
            Port = fixture.BrokerEndpoint.Port,
            Username = fixture.BrokerEndpoint.Username,
            Password = fixture.BrokerEndpoint.Password,
            Queue = queue,
            Exchange = RabbitMqTestFixture.Exchange,
            RoutingKey = string.Empty,
        }),
        Redis,
        parity,
        Options.Create(new MessagingOptions { ServiceName = serviceName }));

    [Fact]
    public async Task Same_message_id_delivered_twice_results_in_exactly_one_ticker_write()
    {
        var message = BuildSpread();
        var id = Guid.NewGuid().ToString();

        await PublishAsync(message, id);
        await PublishAsync(message, id);

        var tickers = fixture.Factory.Services.GetRequiredService<IMongoDatabase>().GetCollection<TradeOpportunityTickerModel>("SpreadsTicker");
        await WaitForCountAsync(tickers, message.Guid, expected: 1);
        await Task.Delay(3000);

        (await tickers.CountDocumentsAsync(Builders<TradeOpportunityTickerModel>.Filter.Eq(x => x.Guid, message.Guid))).Should().Be(1);
    }

    [Fact]
    public async Task Distinct_message_ids_with_identical_content_are_distinct_events()
    {
        // Under content-hash dedupe these would collapse into one; with a message-id each publish is its own event.
        var open = BuildSpread();
        await PublishAsync(open, Guid.NewGuid().ToString());
        var update = BuildSpread(MarketPositionAction.Update, open.Guid, spread: 2.5);
        await PublishAsync(update, Guid.NewGuid().ToString());
        await PublishAsync(update, Guid.NewGuid().ToString());

        var tickers = fixture.Factory.Services.GetRequiredService<IMongoDatabase>().GetCollection<TradeOpportunityTickerModel>("SpreadsTicker");
        await WaitForCountAsync(tickers, open.Guid, expected: 3);
    }

    [Fact]
    public async Task Successful_processing_marks_the_shared_done_key_the_kafka_consumer_will_check()
    {
        var queue = "spread_test_" + Guid.NewGuid().ToString("N")[..8];
        var parity = new CapturingParityRecorder();
        using var service = CreateService(queue, parity, serviceName: "webapp");
        var handled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var message = BuildSpread();
        var id = Guid.NewGuid().ToString();
        service.OnMessageReceived += m => { if (m.Guid == message.Guid) handled.TrySetResult(); return Task.CompletedTask; };

        await service.StartConsumingAsync(CancellationToken.None);
        await PublishAsync(message, id);
        await handled.Task.WaitAsync(TimeSpan.FromSeconds(20));

        await Eventually(async () => (string?)await Redis.GetDatabase().StringGetAsync($"spread-dedupe:webapp:{id}") == "done");
        await service.StopConsumingAsync();
    }

    [Fact]
    public async Task A_failing_handler_releases_the_claim_so_the_message_is_not_lost_and_goes_to_the_dlq()
    {
        var queue = "spread_test_" + Guid.NewGuid().ToString("N")[..8];
        using var service = CreateService(queue, new CapturingParityRecorder(), serviceName: "webapp");
        var message = BuildSpread();
        var id = Guid.NewGuid().ToString();
        var attempts = 0;
        service.OnMessageReceived += m =>
        {
            if (m.Guid == message.Guid)
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("downstream unavailable");
            }

            return Task.CompletedTask;
        };

        try
        {
            await service.StartConsumingAsync(CancellationToken.None);
            await PublishAsync(message, id);

            await Eventually(async () => await DeadLetterCountAsync(queue + "_dlq") >= 1);
            (await Redis.GetDatabase().KeyExistsAsync($"spread-dedupe:webapp:{id}")).Should().BeFalse("a failed claim must be abandoned, not left looking like a duplicate");
            attempts.Should().BeGreaterThan(1, "the handler is retried before dead-lettering");
            await service.StopConsumingAsync();
        }
        finally
        {
            // spread_dlx is one shared fanout: this dead letter also landed in the app's own DLQ, which RabbitMqDeadLetterTests reads.
            await PurgeAsync("spread_api_dlq");
        }
    }

    private async Task PurgeAsync(string queue)
    {
        var factory = new ConnectionFactory { HostName = fixture.BrokerEndpoint.Host, Port = fixture.BrokerEndpoint.Port, UserName = fixture.BrokerEndpoint.Username, Password = fixture.BrokerEndpoint.Password };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueuePurgeAsync(queue);
    }

    [Fact]
    public async Task Parity_records_the_message_id_key_and_producer_timestamp()
    {
        var queue = "spread_test_" + Guid.NewGuid().ToString("N")[..8];
        var parity = new CapturingParityRecorder();
        using var service = CreateService(queue, parity, serviceName: "webapp");
        var message = BuildSpread();
        var id = Guid.NewGuid().ToString();
        var producedAt = new DateTimeOffset(2026, 5, 1, 10, 5, 30, TimeSpan.Zero);
        service.OnMessageReceived += _ => Task.CompletedTask;

        await service.StartConsumingAsync(CancellationToken.None);
        await PublishAsync(message, id, producedAt);
        await Eventually(() => Task.FromResult(parity.Calls.Any(c => c.MessageId == id)));

        var call = parity.Calls.Single(c => c.MessageId == id);
        call.Backend.Should().Be(MessagingBackends.RabbitMq);
        call.Key.Should().Be(SpreadEventKeys.For(message));
        call.ProducedAt.Should().Be(producedAt);
        await service.StopConsumingAsync();
    }

    [Fact]
    public async Task Messages_without_a_message_id_are_not_recorded_for_parity()
    {
        var queue = "spread_test_" + Guid.NewGuid().ToString("N")[..8];
        var parity = new CapturingParityRecorder();
        using var service = CreateService(queue, parity, serviceName: "webapp");
        var handled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var message = BuildSpread();
        service.OnMessageReceived += m => { if (m.Guid == message.Guid) handled.TrySetResult(); return Task.CompletedTask; };

        await service.StartConsumingAsync(CancellationToken.None);
        await PublishAsync(message, messageId: null);
        await handled.Task.WaitAsync(TimeSpan.FromSeconds(20));

        parity.Calls.Should().BeEmpty();
        await service.StopConsumingAsync();
    }

    private async Task<uint> DeadLetterCountAsync(string dlq)
    {
        var factory = new ConnectionFactory { HostName = fixture.BrokerEndpoint.Host, Port = fixture.BrokerEndpoint.Port, UserName = fixture.BrokerEndpoint.Username, Password = fixture.BrokerEndpoint.Password };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        return (await channel.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false)).MessageCount;
    }

    private static async Task Eventually(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(200);
        }
    }

    private static async Task WaitForCountAsync(IMongoCollection<TradeOpportunityTickerModel> tickers, Guid guid, long expected)
    {
        await Eventually(async () => await tickers.CountDocumentsAsync(Builders<TradeOpportunityTickerModel>.Filter.Eq(x => x.Guid, guid)) >= expected);
    }

    private async Task PublishAsync(TradeOpportunityModel message, string? messageId, DateTimeOffset? producedAt = null)
    {
        var factory = new ConnectionFactory { HostName = fixture.BrokerEndpoint.Host, Port = fixture.BrokerEndpoint.Port, UserName = fixture.BrokerEndpoint.Username, Password = fixture.BrokerEndpoint.Password };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(RabbitMqTestFixture.Exchange, ExchangeType.Fanout, durable: true);
        var dlxArgs = new Dictionary<string, object?> { ["x-dead-letter-exchange"] = RabbitMqService.DeadLetterExchange };
        await channel.ExchangeDeclareAsync(RabbitMqService.DeadLetterExchange, ExchangeType.Fanout, durable: true);
        await channel.QueueDeclareAsync(RabbitMqTestFixture.Queue, durable: true, exclusive: false, autoDelete: false, arguments: dlxArgs);
        await channel.QueueBindAsync(RabbitMqTestFixture.Queue, RabbitMqTestFixture.Exchange, routingKey: string.Empty);

        var properties = new BasicProperties();
        if (messageId is not null)
        {
            properties.MessageId = messageId;
            properties.Timestamp = new AmqpTimestamp((producedAt ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds());
        }

        await channel.BasicPublishAsync(RabbitMqTestFixture.Exchange, routingKey: string.Empty, mandatory: false, basicProperties: properties, body: TradeOpportunitySerializer.Serialize(message));
    }

    private static TradeOpportunityModel BuildSpread(MarketPositionAction actionType = MarketPositionAction.Open, Guid? guid = null, double spread = 2.25)
    {
        ExchangeRateModel Rate(string exchange) => new() { Symbol = "BTC/USDT", Exchange = exchange, ExchangeRate = 50_000, VolumeAsk = 100, VolumeBid = 100 };

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
