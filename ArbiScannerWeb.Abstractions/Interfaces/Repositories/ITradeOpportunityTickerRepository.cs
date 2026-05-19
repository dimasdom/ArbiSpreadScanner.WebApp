using ArbiScannerWeb.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Abstractions.Interfaces.Repositories
{
    public interface ITradeOpportunityTickerRepository
    {
        Task InsertAsync(TradeOpportunityTickerModel ticker);
        Task DeleteAllByGuidAsync(Guid guid);
        Task<TradeOpportunityTickerModel?> GetLatestByGuidAsync(Guid guid);
        Task<List<TradeOpportunityTickerModel>> GetRemainingWithoutOrderBookAsync(Guid guid, int skip, int limit);
    }
}
