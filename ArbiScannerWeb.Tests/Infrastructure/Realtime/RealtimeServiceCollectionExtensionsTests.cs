using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Infrastructure.Realtime;
using ArbiScannerWeb.Infrastructure.Settings;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ArbiScannerWeb.Tests.Infrastructure.Realtime;

public class RealtimeServiceCollectionExtensionsTests
{
    private static ServiceProvider Build(Dictionary<string, string?>? config = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddLogging();
        services.AddSingleton(Mock.Of<ISubscriptionService>());
        services.AddSingleton(Mock.Of<ITradeOpportunityRepository>());
        services.AddSingleton(Mock.Of<ITradeOpportunityTickerRepository>());
        services.AddSpreadStream(new ConfigurationBuilder().AddInMemoryCollection(config ?? []).Build());
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void ResolvesTheWholePipeline()
    {
        using var provider = Build();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISpreadStreamService>().Should().BeOfType<SpreadStreamService>();
        provider.GetRequiredService<IRealtimeNotifier>().Should().BeOfType<SseNotifier>();
    }

    [Fact]
    public void BrokerIsASingletonSharedByTheNotifierAndTheStreams()
    {
        using var provider = Build();

        provider.GetRequiredService<ISpreadStreamBroker>().Should().BeSameAs(provider.GetRequiredService<ISpreadStreamBroker>());
    }

    [Fact]
    public void TheRealtimeNotifierIsASingletonSoItCanBeInjectedIntoScopedServices()
    {
        using var provider = Build();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        first.ServiceProvider.GetRequiredService<IRealtimeNotifier>()
            .Should().BeSameAs(second.ServiceProvider.GetRequiredService<IRealtimeNotifier>());
    }

    [Fact]
    public async Task NotifierPublishesIntoTheSameBrokerTheStreamsSubscribeTo()
    {
        using var provider = Build();
        var broker = provider.GetRequiredService<ISpreadStreamBroker>();
        var spread = Guid.NewGuid();
        using var subscription = broker.Subscribe(spread);

        await provider.GetRequiredService<IRealtimeNotifier>()
            .NotifyGroupAsync(spread.ToString(), new ArbiScannerWeb.Domain.Models.DTOs.MessageDto { Ticker = new ArbiScannerWeb.Domain.Models.TradeOpportunityTickerModel() });

        subscription.Reader.TryRead(out var received).Should().BeTrue();
        received!.Type.Should().Be(ArbiScannerWeb.Domain.Models.DTOs.SpreadStreamEventTypes.Ticker);
    }

    [Fact]
    public void OptionsDefaultWhenUnconfigured_AndBindFromTheSpreadStreamSection()
    {
        using var defaults = Build();
        defaults.GetRequiredService<IOptions<SpreadStreamOptions>>().Value.KeepAliveSeconds.Should().Be(15);

        using var configured = Build(new()
        {
            ["SpreadStream:KeepAliveSeconds"] = "5",
            ["SpreadStream:MaxLifetimeMinutes"] = "2.5",
            ["SpreadStream:ReplayLimit"] = "10",
            ["SpreadStream:SubscriberBufferSize"] = "32",
        });
        var bound = configured.GetRequiredService<IOptions<SpreadStreamOptions>>().Value;
        (bound.KeepAliveSeconds, bound.MaxLifetimeMinutes, bound.ReplayLimit, bound.SubscriberBufferSize).Should().Be((5, 2.5, 10, 32));
    }
}
