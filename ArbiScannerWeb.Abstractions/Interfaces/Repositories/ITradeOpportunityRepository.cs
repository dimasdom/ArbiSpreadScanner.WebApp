using ArbiScannerWeb.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Abstractions.Interfaces.Repositories
{
    public interface ITradeOpportunityRepository
    {
        Task UpsertAsync(TradeOpportunityModel model);
        Task SetStatusClosedAsync(Guid guid);
        Task<List<TradeOpportunityModel>> GetAllAsync();
        Task<TradeOpportunityModel?> GetByGuidAsync(Guid guid);
        Task ReplaceAsync(TradeOpportunityModel model);
        Task<List<TradeOpportunityModel>> GetOpenByTypeAsync(SpreadType type);
        Task<List<TradeOpportunityModel>> GetByTypeAsync(SpreadType type);

        /// <summary>
        /// Returns every spread (open and closed) with the order-book level arrays
        /// (Bids/AsksExchangeA/B) excluded, for aggregation reads that only need the
        /// summary fields — avoids pulling potentially large embedded arrays into
        /// memory for every document.
        /// </summary>
        Task<List<TradeOpportunityModel>> GetAllForStatsAsync();
    }
}
