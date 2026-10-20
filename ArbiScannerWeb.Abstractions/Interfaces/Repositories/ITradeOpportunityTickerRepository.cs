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
        Task<long> DeleteOrphanedAsync(IEnumerable<Guid> openGuids);
        Task<TradeOpportunityTickerModel?> GetLatestByGuidAsync(Guid guid);
        Task<List<TradeOpportunityTickerModel>> GetRemainingWithoutOrderBookAsync(Guid guid, int skip, int limit);

        /// <summary>
        /// Tickers recorded strictly after <paramref name="since"/>, oldest first, without order books (a reconnecting SSE client
        /// only needs the chart points it missed). When more than <paramref name="limit"/> qualify, the <b>newest</b>
        /// <paramref name="limit"/> are returned so the replay ends at the live edge rather than leaving a gap before it.
        /// </summary>
        Task<List<TradeOpportunityTickerModel>> GetSinceWithoutOrderBookAsync(Guid guid, DateTime since, int limit);
    }
}
