using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Messaging;
using ArbiScannerWeb.Infrastructure.Messaging;
using ArbiScannerWeb.Infrastructure.Settings;
using ArbiScannerWeb.Messaging.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace ArbiScannerWeb.Tests.Messaging;

public class SpreadMessagingExtensionsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value)).Build();

    private static ServiceProvider Build(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new Mock<IConnectionMultiplexer>().Object);
        services.AddScoped(_ => new Mock<ITradeOpportunityService>().Object);
        services.AddSpreadMessaging<TradeOpportunityKafkaHandler>(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Default_mode_is_RabbitMq_and_registers_nothing_Kafka_related()
    {
        using var provider = Build(Config());

        provider.GetRequiredService<IParityRecorder>().Should().BeOfType<NoOpParityRecorder>();
        provider.GetServices<IHostedService>().Should().BeEmpty();
        provider.GetService<IKafkaMessageProducer>().Should().BeNull("a Kafka outage or bad config must not be able to affect a RabbitMQ-only service");
    }

    [Fact]
    public void Shadow_mode_registers_the_shadow_consumer_group_comparer_and_redis_recorder()
    {
        using var provider = Build(Config(
            ("Messaging:ServiceName", "webapp"),
            ("Messaging:Consumer:Mode", "Shadow"),
            ("Kafka:BootstrapServers", "kafka:9092")));

        provider.GetRequiredService<IParityRecorder>().Should().BeOfType<RedisParityRecorder>();
        provider.GetServices<IHostedService>().Select(s => s.GetType()).Should().BeEquivalentTo([typeof(KafkaConsumerService), typeof(ParityComparerService)]);
        provider.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value.GroupId.Should().Be("webapp-spreads-shadow");
    }

    [Fact]
    public void Shadow_mode_requires_a_service_name()
    {
        var act = () => Build(Config(("Messaging:Consumer:Mode", "Shadow")));

        act.Should().Throw<InvalidOperationException>().WithMessage("*ServiceName*");
    }

    [Fact]
    public void Kafka_mode_registers_only_the_live_consumer_in_the_live_group()
    {
        using var provider = Build(Config(
            ("Messaging:ServiceName", "webapp"),
            ("Messaging:Consumer:Mode", "Kafka"),
            ("Kafka:BootstrapServers", "kafka:9092")));

        provider.GetServices<IHostedService>().Select(s => s.GetType()).Should().BeEquivalentTo([typeof(KafkaConsumerService)], "no parity comparer once RabbitMQ is out of the picture");
        provider.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value.GroupId.Should().Be("webapp-spreads");
        provider.GetRequiredService<IParityRecorder>().Should().BeOfType<NoOpParityRecorder>();
        provider.GetRequiredService<IIdempotencyStore>().Should().BeOfType<RedisIdempotencyStore>();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IKafkaMessageHandler>().Should().BeOfType<TradeOpportunityKafkaHandler>();
    }

    [Fact]
    public void Kafka_mode_requires_a_service_name()
    {
        var act = () => Build(Config(("Messaging:Consumer:Mode", "Kafka")));

        act.Should().Throw<InvalidOperationException>().WithMessage("*ServiceName*");
    }

    [Theory]
    [InlineData(null, MessagingConsumerMode.RabbitMq)]
    [InlineData("Shadow", MessagingConsumerMode.Shadow)]
    [InlineData("kafka", MessagingConsumerMode.Kafka)]
    [InlineData("nonsense", MessagingConsumerMode.RabbitMq)]
    public void GetConsumerMode_reads_the_configured_mode_and_defaults_to_RabbitMq(string? value, MessagingConsumerMode expected)
    {
        var config = value is null ? Config() : Config(("Messaging:Consumer:Mode", value));

        config.GetConsumerMode().Should().Be(expected);
    }

    [Fact]
    public void Shadow_group_ids_follow_the_service_name()
    {
        SpreadMessagingExtensions.ShadowGroupId("telegram").Should().Be("telegram-spreads-shadow");
        SpreadMessagingExtensions.LiveGroupId("telegram").Should().Be("telegram-spreads");
    }
}
