using ArbiScannerWeb.Domain.Models;

namespace ArbiScannerWeb.Tests.Messaging;

internal static class MessagingTestData
{
    public static TradeOpportunityModel Spread(
        SpreadType type = SpreadType.Futures,
        MarketPositionAction action = MarketPositionAction.Open,
        string exchangeA = "Binance",
        string exchangeB = "Bybit",
        string symbol = "BTC/USDT:USDT",
        bool aIsLong = true)
    {
        ExchangeRateModel Rate(string exchange) => new() { Symbol = symbol, Exchange = exchange, ExchangeRate = 50_000, VolumeAsk = 100, VolumeBid = 100 };

        var a = Rate(exchangeA);
        var b = Rate(exchangeB);
        return new TradeOpportunityModel
        {
            Guid = Guid.NewGuid(),
            Symbol = symbol,
            Type = type,
            ActionType = action,
            ExchangeRateA = a,
            ExchangeRateB = b,
            ExchangeLong = aIsLong ? a : b,
            ExchangeShort = aIsLong ? b : a,
            Spread = 2.25,
            StartSpread = 2.25,
        };
    }
}
