namespace ArbiScannerWeb.Domain.Models;

public class SpreadStatsSnapshotModel
{
    public Guid Id { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public int TotalSpreadsAnalyzed { get; set; }

    public required List<SymbolAverageSpreadEntry> TopSymbolsByAverageSpread { get; set; }
    public required List<ExchangeCountEntry> TopExchangesByCount { get; set; }
    public required List<ExchangePairCountEntry> TopExchangePairsByCount { get; set; }
    public required List<ExchangeMedianVolumeEntry> MedianVolumeByExchange { get; set; }
    public required List<SpreadTypeCountEntry> SpreadTypeDistribution { get; set; }
    public required List<SymbolCountEntry> TopSymbolsByCount { get; set; }
}

public class SymbolAverageSpreadEntry
{
    public required string Symbol { get; set; }
    public double AverageSpreadPercent { get; set; }
    public int SampleCount { get; set; }
}

public class ExchangeCountEntry
{
    public required string Exchange { get; set; }
    public int Count { get; set; }
}

public class ExchangePairCountEntry
{
    public required string ExchangeA { get; set; }
    public required string ExchangeB { get; set; }
    public int Count { get; set; }
}

public class ExchangeMedianVolumeEntry
{
    public required string Exchange { get; set; }
    public double MedianVolume { get; set; }
    public int SampleCount { get; set; }
}

public class SpreadTypeCountEntry
{
    public SpreadType Type { get; set; }
    public int Count { get; set; }
}

public class SymbolCountEntry
{
    public required string Symbol { get; set; }
    public int Count { get; set; }
}
