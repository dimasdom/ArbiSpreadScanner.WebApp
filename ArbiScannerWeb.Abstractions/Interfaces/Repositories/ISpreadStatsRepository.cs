using ArbiScannerWeb.Domain.Models;

namespace ArbiScannerWeb.Abstractions.Interfaces.Repositories
{
    public interface ISpreadStatsRepository
    {
        Task InsertSnapshotAsync(SpreadStatsSnapshotModel model);
        Task<SpreadStatsSnapshotModel?> GetLatestAsync();
        Task<SpreadStatsSnapshotModel?> GetByIdAsync(Guid id);
        Task<List<SnapshotIndexEntry>> GetIndexAsync();
    }
}
