using ArbiScannerWeb.Domain.Models;

namespace ArbiScannerWeb.Tests.Infrastructure.Realtime;

internal static class TestSpreads
{
    /// <summary>A minimal valid spread: the model has a handful of required members the realtime tests don't care about.</summary>
    public static TradeOpportunityModel Build(
        Guid? guid = null,
        OrderStatus status = OrderStatus.Open,
        MarketPositionAction action = MarketPositionAction.Update)
    {
        ExchangeRateModel Rate(string exchange) => new() { Symbol = "BTC/USDT", Exchange = exchange };

        return new TradeOpportunityModel
        {
            Guid = guid ?? Guid.NewGuid(),
            Symbol = "BTC/USDT",
            OrderStatus = status,
            ActionType = action,
            ExchangeRateA = Rate("Binance"),
            ExchangeRateB = Rate("Bybit"),
            ExchangeLong = Rate("Bybit"),
            ExchangeShort = Rate("Binance"),
        };
    }
}
