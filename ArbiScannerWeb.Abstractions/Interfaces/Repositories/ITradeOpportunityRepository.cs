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
    }
}
