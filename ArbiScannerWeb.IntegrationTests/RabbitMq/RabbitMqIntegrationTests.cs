using System.Net.Http.Json;
using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.IntegrationTests.Fixtures;
using ArbiScannerWeb.IntegrationTests.Support;
using ArbiScannerWeb.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace ArbiScannerWeb.IntegrationTests.RabbitMq;

[Collection(RabbitMqCollection.Name)]
public class RabbitMqIntegrationTests(RabbitMqTestFixture fixture)
{
    [Fact]
    public async Task PublishedMessage_IsDeserializedByRabbitMqService()
    {
        var rabbitMqService = fixture.Factory.Services.GetRequiredService<IRabbitMqService>();
        var message = BuildSpread(MarketPositionAction.Open);
        var received = new TaskCompletionSource<TradeOpportunityModel>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task Handler(TradeOpportunityModel model)
        {
            if (model.Guid == message.Guid)
                received.TrySetResult(model);
            return Task.CompletedTask;
        }

        rabbitMqService.OnMessageReceived += Handler;
        try
        {
            await PublishAsync(message);

            var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(20)));
            completed.Should().Be(received.Task, "RabbitMqService should have consumed and deserialized the published message");

            var deserialized = await received.Task;
            deserialized.Symbol.Should().Be(message.Symbol);
            deserialized.Spread.Should().Be(message.Spread);
            deserialized.ActionType.Should().Be(MarketPositionAction.Open);
            deserialized.ExchangeRateA.Exchange.Should().Be(message.ExchangeRateA.Exchange);
        }
        finally
        {
            rabbitMqService.OnMessageReceived -= Handler;
        }
    }

    [Fact]
    public async Task PublishedOpenMessage_FlowsEndToEndIntoMongoAndIsServedByApi()
    {
        var message = BuildSpread(MarketPositionAction.Open);
        await PublishAsync(message);

        var authedClient = fixture.Factory.CreateAuthenticatedClient();

        var deadline = DateTime.UtcNow.AddSeconds(20);
        ApiResult<TradeOpportunityDetailsDto>? result = null;
        while (DateTime.UtcNow < deadline)
        {
            var response = await authedClient.GetAsync($"/api/TradeOpportunity/GetSpreadInfo/{message.Guid}");
            result = await response.Content.ReadFromJsonAsync<ApiResult<TradeOpportunityDetailsDto>>(JsonOptions.CaseInsensitive);
            if (result?.IsSuccess == true)
                break;

            await Task.Delay(500);
        }

        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue("the RabbitMQ message should have been consumed and persisted via MessageProcessingService -> TradeOpportunityService");
        result.Value!.PositionModel.Guid.Should().Be(message.Guid);
        result.Value.PositionModel.Symbol.Should().Be(message.Symbol);
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

        await channel.ExchangeDeclareAsync(RabbitMqService.DeadLetterExchange, ExchangeType.Fanout, durable: true);
        await channel.ExchangeDeclareAsync(RabbitMqTestFixture.Exchange, ExchangeType.Fanout, durable: true);
        await channel.QueueDeclareAsync(RabbitMqTestFixture.Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = RabbitMqService.DeadLetterExchange });
        await channel.QueueBindAsync(RabbitMqTestFixture.Queue, RabbitMqTestFixture.Exchange, routingKey: "");

        var body = ProtobufTestSerializer.Serialize(message);
        await channel.BasicPublishAsync(RabbitMqTestFixture.Exchange, routingKey: "", body: body);
    }

    private static TradeOpportunityModel BuildSpread(MarketPositionAction actionType)
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
            Guid = Guid.NewGuid(),
            Symbol = "BTC/USDT",
            Type = SpreadType.Spot,
            ActionType = actionType,
            ExchangeRateA = Rate("Binance"),
            ExchangeRateB = Rate("Bybit"),
            ExchangeShort = Rate("Binance"),
            ExchangeLong = Rate("Bybit"),
            Spread = 2.25,
            StartSpread = 2.25,
            SummaryTarrif = 0.1,
            PossibleProfit = 15,
        };
    }
}
