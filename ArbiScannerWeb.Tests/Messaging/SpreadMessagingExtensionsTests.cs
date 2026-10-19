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
    public void Registers_the_live_consumer_in_the_service_group_with_its_handler_and_idempotency_store()
    {
        using var provider = Build(Config(("Messaging:ServiceName", "webapp"), ("Kafka:BootstrapServers", "kafka:9092")));

        provider.GetServices<IHostedService>().Select(s => s.GetType()).Should().BeEquivalentTo([typeof(KafkaConsumerService)]);
        provider.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value.GroupId.Should().Be("webapp-spreads");
        provider.GetRequiredService<IOptions<KafkaOptions>>().Value.BootstrapServers.Should().Be("kafka:9092");
        provider.GetRequiredService<IIdempotencyStore>().Should().BeOfType<RedisIdempotencyStore>();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IKafkaMessageHandler>().Should().BeOfType<TradeOpportunityKafkaHandler>();
    }

    [Fact]
    public void The_group_follows_the_service_name_even_if_the_config_is_applied_after_registration()
    {
        // Test hosts add their overrides after Program.cs ran, so nothing may be read eagerly.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new Mock<IConnectionMultiplexer>().Object);
        services.AddScoped(_ => new Mock<ITradeOpportunityService>().Object);
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSpreadMessaging<TradeOpportunityKafkaHandler>(config);
        config["Messaging:ServiceName"] = "telegram";

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value.GroupId.Should().Be("telegram-spreads");
    }

    [Fact]
    public void A_missing_service_name_fails_validation_with_a_clear_message()
    {
        using var provider = Build(Config());

        var act = () => provider.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().WithMessage("*Messaging:ServiceName*");
    }

    [Fact]
    public void A_disabled_consumer_needs_no_service_name_and_does_not_start()
    {
        using var provider = Build(Config(("Kafka:Consumer:Enabled", "false")));

        provider.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value.Enabled.Should().BeFalse();
        var consumer = provider.GetServices<IHostedService>().OfType<KafkaConsumerService>().Single();
        var started = consumer.StartAsync(CancellationToken.None);

        started.IsCompletedSuccessfully.Should().BeTrue("a disabled consumer returns immediately and never connects to a broker");
    }

    [Fact]
    public void Group_ids_follow_the_service_name()
    {
        SpreadMessagingExtensions.LiveGroupId("telegram").Should().Be("telegram-spreads");
        SpreadMessagingExtensions.LiveGroupId(" ").Should().BeEmpty();
    }
}
