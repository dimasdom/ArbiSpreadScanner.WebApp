using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Domain.Models.DTOs
{
    public class TradeOpportunityDetailsDto
    {
        public required TradeOpportunityModel PositionModel { get; set; }

        public required List<TradeOpportunityTickerModel> Tickers { get; set; }

        public string GroupName { get; set; } = string.Empty;

        /// <summary>URL for the short exchange based on spread type.</summary>
        public string? ShortExchangeUrl { get; set; }

        /// <summary>URL for the long exchange based on spread type.</summary>
        public string? LongExchangeUrl { get; set; }

        /// <summary>Only populated by GetSpreadInfo (needs ticker history) - null for the
        /// list endpoints (GetSpreadsForUser, GetRecommendedSpreads).</summary>
        public SpreadAnalysisDto? Analysis { get; set; }
    }
}
