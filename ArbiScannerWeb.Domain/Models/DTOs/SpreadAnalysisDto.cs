namespace ArbiScannerWeb.Domain.Models.DTOs
{
    /// <summary>
    /// A deterministic, server-computed verdict on whether a spread is currently worth
    /// taking - only populated on <see cref="TradeOpportunityDetailsDto"/> instances returned
    /// by GetSpreadInfo, since it needs the ticker history that GetSpreadsForUser doesn't load.
    /// </summary>
    public class SpreadAnalysisDto
    {
        public bool Recommended { get; set; }

        public List<string> Reasons { get; set; } = new();

        /// <summary>Only set for Funding-type spreads whose recent ticker history is falling
        /// fast enough that fees/slippage may exceed the funding collected by payout time.</summary>
        public string? TrendWarning { get; set; }
    }
}
