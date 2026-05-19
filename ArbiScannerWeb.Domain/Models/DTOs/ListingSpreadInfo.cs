using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Domain.Models.DTOs
{
    public class ListingSpreadInfo
    {
        public required TradeOpportunityModel PositionModel { get; set; }
        public required List<TradeOpportunityTickerModel> possiblePositionTickerModels { get; set; }
    }
}
