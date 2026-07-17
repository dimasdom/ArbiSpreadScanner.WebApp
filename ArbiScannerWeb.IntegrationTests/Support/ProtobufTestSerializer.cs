using ArbiScannerWeb.Domain.Models;
using ProtoBuf.Meta;

namespace ArbiScannerWeb.IntegrationTests.Support;

internal static class ProtobufTestSerializer
{
    private static readonly RuntimeTypeModel Model = CreateModel();

    public static byte[] Serialize(TradeOpportunityModel message)
    {
        using var stream = new MemoryStream();
        Model.Serialize(stream, message);
        return stream.ToArray();
    }

    private static RuntimeTypeModel CreateModel()
    {
        var model = RuntimeTypeModel.Create();

        model.Add(typeof(ExchangeRateModel), true);

        var orderLevelMeta = model.Add(typeof(OrderLevelModel), false);
        orderLevelMeta.Add(1, nameof(OrderLevelModel.Price));
        orderLevelMeta.Add(2, nameof(OrderLevelModel.Amount));

        var tradeOpportunityMeta = model.Add(typeof(TradeOpportunityModel), false);
        tradeOpportunityMeta.Add(1, nameof(TradeOpportunityModel.Guid));
        tradeOpportunityMeta.Add(2, nameof(TradeOpportunityModel.ExchangeRateA));
        tradeOpportunityMeta.Add(3, nameof(TradeOpportunityModel.ExchangeRateB));
        tradeOpportunityMeta.Add(4, nameof(TradeOpportunityModel.ExchangeShort));
        tradeOpportunityMeta.Add(5, nameof(TradeOpportunityModel.ExchangeLong));
        tradeOpportunityMeta.Add(7, nameof(TradeOpportunityModel.SummaryTarrif));
        tradeOpportunityMeta.Add(8, nameof(TradeOpportunityModel.PossibleProfit));
        tradeOpportunityMeta.Add(9, nameof(TradeOpportunityModel.TotalFunding));
        tradeOpportunityMeta.Add(10, nameof(TradeOpportunityModel.Spread));
        tradeOpportunityMeta.Add(11, nameof(TradeOpportunityModel.Type));
        tradeOpportunityMeta.Add(12, nameof(TradeOpportunityModel.ActionType));
        tradeOpportunityMeta.Add(13, nameof(TradeOpportunityModel.StartSpread));
        tradeOpportunityMeta.Add(14, nameof(TradeOpportunityModel.Symbol));
        tradeOpportunityMeta.Add(15, nameof(TradeOpportunityModel.BidsExchangeA));
        tradeOpportunityMeta.Add(16, nameof(TradeOpportunityModel.AsksExchangeA));
        tradeOpportunityMeta.Add(17, nameof(TradeOpportunityModel.BidsExchangeB));
        tradeOpportunityMeta.Add(18, nameof(TradeOpportunityModel.AsksExchangeB));
        tradeOpportunityMeta.Add(19, nameof(TradeOpportunityModel.DateTime));

        return model;
    }
}
