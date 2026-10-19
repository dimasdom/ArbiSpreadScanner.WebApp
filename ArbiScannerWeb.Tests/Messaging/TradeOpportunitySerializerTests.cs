using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Messaging;
using FluentAssertions;

namespace ArbiScannerWeb.Tests.Messaging;

public class TradeOpportunitySerializerTests
{
    [Fact]
    public void Round_trips_the_fields_consumers_depend_on()
    {
        var original = MessagingTestData.Spread(SpreadType.Funding, MarketPositionAction.Close);
        original.BidsExchangeA = [new OrderLevelModel { Price = 1.5, Amount = 2 }];

        var copy = TradeOpportunitySerializer.Deserialize(TradeOpportunitySerializer.Serialize(original));

        copy.Guid.Should().Be(original.Guid);
        copy.Type.Should().Be(SpreadType.Funding);
        copy.ActionType.Should().Be(MarketPositionAction.Close);
        copy.Symbol.Should().Be(original.Symbol);
        copy.ExchangeRateA.Exchange.Should().Be("Binance");
        copy.ExchangeLong.Exchange.Should().Be("Binance");
        copy.BidsExchangeA.Should().ContainSingle().Which.Price.Should().Be(1.5);
    }

    [Fact]
    public void Garbage_is_rejected()
    {
        var act = () => TradeOpportunitySerializer.Deserialize([0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);

        act.Should().Throw<Exception>();
    }
}
