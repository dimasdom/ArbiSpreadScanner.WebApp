using ArbiScannerWeb.Domain.Models;

namespace ArbiScannerWeb.Infrastructure.Messaging;

public static class SpreadEventKeys
{
    /// <summary>Kafka key for any Open/Update/Close event of this opportunity, derived exactly as the scanner derives it
    /// (stable A/B exchanges, never the long/short legs).</summary>
    public static string For(TradeOpportunityModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var type = model.Type switch
        {
            SpreadType.Futures => SpreadKeyType.Futures,
            SpreadType.Funding => SpreadKeyType.Funding,
            SpreadType.Spot => SpreadKeyType.Spot,
            _ => throw new ArgumentOutOfRangeException(nameof(model), model.Type, "Unknown spread type."),
        };

        return SpreadKey.Build(type, model.ExchangeRateA.Exchange, model.ExchangeRateB.Exchange, model.ExchangeRateA.Symbol);
    }
}
