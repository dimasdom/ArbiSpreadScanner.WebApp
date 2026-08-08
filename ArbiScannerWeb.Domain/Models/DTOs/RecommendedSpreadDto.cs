namespace ArbiScannerWeb.Domain.Models.DTOs
{
    /// <summary>One of the up-to-2-per-category picks returned by GetRecommendedSpreads.</summary>
    public class RecommendedSpreadDto
    {
        public required TradeOpportunityDetailsDto Details { get; set; }

        public SpreadType Category { get; set; }
    }
}
