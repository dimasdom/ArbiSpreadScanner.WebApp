using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Settings;
using MongoDB.Driver;

namespace ArbiScannerWeb.Infrastructure.Repositories
{
    public class SpreadStatsRepositoryMongo : ISpreadStatsRepository
    {
        private readonly IMongoCollection<SpreadStatsSnapshotModel> _collection;

        public SpreadStatsRepositoryMongo(IMongoDatabase mongoDatabase, MongoDbSettings mongoSettings)
        {
            _collection = mongoDatabase.GetCollection<SpreadStatsSnapshotModel>(mongoSettings.SpreadStatsSnapshotsCollection);

            _collection.Indexes.CreateOne(
                new CreateIndexModel<SpreadStatsSnapshotModel>(
                    Builders<SpreadStatsSnapshotModel>.IndexKeys.Descending(x => x.GeneratedAtUtc),
                    new CreateIndexOptions { Background = true, Name = "idx_generatedatutc_desc" }));
        }

        public Task InsertSnapshotAsync(SpreadStatsSnapshotModel model)
            => _collection.InsertOneAsync(model);

        public Task<SpreadStatsSnapshotModel?> GetLatestAsync()
            => _collection
                .Find(Builders<SpreadStatsSnapshotModel>.Filter.Empty)
                .SortByDescending(x => x.GeneratedAtUtc)
                .Limit(1)
                .FirstOrDefaultAsync()!;

        public Task<SpreadStatsSnapshotModel?> GetByIdAsync(Guid id)
            => _collection.Find(Builders<SpreadStatsSnapshotModel>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync()!;

        public async Task<List<SnapshotIndexEntry>> GetIndexAsync()
        {
            var projection = Builders<SpreadStatsSnapshotModel>.Projection
                .Expression(x => new SnapshotIndexEntry { Id = x.Id, GeneratedAtUtc = x.GeneratedAtUtc });

            return await _collection
                .Find(Builders<SpreadStatsSnapshotModel>.Filter.Empty)
                .SortByDescending(x => x.GeneratedAtUtc)
                .Project(projection)
                .ToListAsync();
        }
    }
}
