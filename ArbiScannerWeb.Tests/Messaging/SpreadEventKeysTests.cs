using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Messaging;
using FluentAssertions;

namespace ArbiScannerWeb.Tests.Messaging;

public class SpreadEventKeysTests
{
    [Fact]
    public void Key_does_not_change_when_the_long_and_short_legs_flip()
    {
        var before = SpreadEventKeys.For(MessagingTestData.Spread(aIsLong: true));
        var after = SpreadEventKeys.For(MessagingTestData.Spread(aIsLong: false));

        after.Should().Be(before);
    }

    [Fact]
    public void Key_matches_the_scanner_side_golden_example()
    {
        // The same inputs are asserted in ArbitrageScanner's SpreadEventKeysTests: both repos must derive the same key.
        SpreadEventKeys.For(MessagingTestData.Spread(exchangeA: "Bybit", exchangeB: "Binance"))
            .Should().Be("futures:binance:bybit:BTC-USDT.USDT");
    }

    [Theory]
    [InlineData(SpreadType.Futures, "futures")]
    [InlineData(SpreadType.Funding, "funding")]
    [InlineData(SpreadType.Spot, "spot")]
    public void Key_is_prefixed_with_the_spread_type(SpreadType type, string prefix)
    {
        SpreadEventKeys.For(MessagingTestData.Spread(type: type)).Should().StartWith(prefix + ":");
    }
}
