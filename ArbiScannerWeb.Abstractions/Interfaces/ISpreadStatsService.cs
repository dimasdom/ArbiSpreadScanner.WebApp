using ArbiScannerWeb.Domain.Models;
using FluentResults;

namespace ArbiScannerWeb.Abstractions.Interfaces
{
    public interface ISpreadStatsService
    {
        Task<Result<SpreadStatsSnapshotModel>> GetLatestAsync();
        Task<Result<SpreadStatsSnapshotModel>> GetByIdAsync(Guid id);
        Task<Result<List<SnapshotIndexEntry>>> GetSnapshotIndexAsync();

        /// <summary>
        /// Recomputes the 6 stats over the full current state of CurrentSpreads,
        /// persists the result as a new snapshot, and refreshes the Redis cache.
        /// Invoked by SpreadStatsAggregationJob on its daily cron schedule.
        /// </summary>
        Task<Result<SpreadStatsSnapshotModel>> GenerateAndPersistSnapshotAsync();
    }
}
