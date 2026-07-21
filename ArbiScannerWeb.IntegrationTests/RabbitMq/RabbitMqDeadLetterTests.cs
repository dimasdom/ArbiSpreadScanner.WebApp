using ArbiScannerWeb.IntegrationTests.Fixtures;
using ArbiScannerWeb.Infrastructure.Services;
using FluentAssertions;
using RabbitMQ.Client;

namespace ArbiScannerWeb.IntegrationTests.RabbitMq;

[Collection(RabbitMqCollection.Name)]
public class RabbitMqDeadLetterTests(RabbitMqTestFixture fixture)
{
    [Fact]
    public async Task PoisonMessage_LandsInDeadLetterQueue_InsteadOfRequeueingForever()
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

        var dlqQueueName = $"{RabbitMqTestFixture.Queue}_dlq";
        var dlxArgs = new Dictionary<string, object?> { ["x-dead-letter-exchange"] = RabbitMqService.DeadLetterExchange };

        await channel.ExchangeDeclareAsync(RabbitMqService.DeadLetterExchange, ExchangeType.Fanout, durable: true);
        await channel.QueueDeclareAsync(dlqQueueName, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(dlqQueueName, RabbitMqService.DeadLetterExchange, routingKey: "");

        await channel.ExchangeDeclareAsync(RabbitMqTestFixture.Exchange, ExchangeType.Fanout, durable: true);
        await channel.QueueDeclareAsync(RabbitMqTestFixture.Queue, durable: true, exclusive: false, autoDelete: false, arguments: dlxArgs);
        await channel.QueueBindAsync(RabbitMqTestFixture.Queue, RabbitMqTestFixture.Exchange, routingKey: "");

        // Not a valid protobuf TradeOpportunityModel — RabbitMqService.Deserialize will throw,
        // and (after the retry pipeline can't help a poison message) it should be nacked with
        // requeue:false straight to the DLQ rather than looping forever.
        var poisonBody = "this is not protobuf"u8.ToArray();
        await channel.BasicPublishAsync(RabbitMqTestFixture.Exchange, routingKey: "", body: poisonBody);

        var deadline = DateTime.UtcNow.AddSeconds(20);
        BasicGetResult? dlqMessage = null;
        while (DateTime.UtcNow < deadline)
        {
            dlqMessage = await channel.BasicGetAsync(dlqQueueName, autoAck: true);
            if (dlqMessage is not null)
                break;

            await Task.Delay(500);
        }

        dlqMessage.Should().NotBeNull("a poison message should be dead-lettered instead of requeued forever");
        dlqMessage!.Body.ToArray().Should().BeEquivalentTo(poisonBody);
    }
}
