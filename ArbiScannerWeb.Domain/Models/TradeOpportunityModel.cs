namespace ArbiScannerWeb.Domain.Models;

public class TradeOpportunityModel
{
    public Guid Guid { get; set; }
    public required ExchangeRateModel ExchangeRateA { get; set; }
    public required ExchangeRateModel ExchangeRateB { get; set; }
    public required ExchangeRateModel ExchangeShort { get; set; }
    public required ExchangeRateModel ExchangeLong { get; set; }
    public double Volatility { get; set; }
    public double SummaryTarrif { get; set; }
    public double PossibleProfit { get; set; }
    public double TotalFunding { get; set; }
    public double Spread { get; set; }
    public SpreadType Type { get; set; }
    public MarketPositionAction ActionType { get; set; }
    public double StartSpread { get; set; }
    public required string Symbol { get; set; }
    public List<OrderLevelModel>? BidsExchangeA { get; set; }
    public List<OrderLevelModel>? AsksExchangeA { get; set; }
    public List<OrderLevelModel>? BidsExchangeB { get; set; }
    public List<OrderLevelModel>? AsksExchangeB { get; set; }
    public DateTime DateTime { get; set; }
    public OrderStatus OrderStatus { get; set; } = OrderStatus.Open;
}
