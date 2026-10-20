using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Settings;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Abstractions.Interfaces.Repositories
{
    public class TradeOpportunityTickerRepositoryMongo : ITradeOpportunityTickerRepository
    {
        private readonly IMongoCollection<TradeOpportunityTickerModel> _collection;

        public TradeOpportunityTickerRepositoryMongo(IMongoDatabase mongoDatabase, MongoDbSettings mongoSettings)
        {
            _collection = mongoDatabase.GetCollection<TradeOpportunityTickerModel>(mongoSettings.SpreadsTickerCollection);

            // Compound index: covers all filter-by-Guid + sort-by-DateTime queries.
            // Using the typed collection guarantees the field names and BSON types
            // match the stored documents exactly, so the index is always used.
            _collection.Indexes.CreateMany([
                new CreateIndexModel<TradeOpportunityTickerModel>(
                    Builders<TradeOpportunityTickerModel>.IndexKeys
                        .Ascending(x => x.Guid)
                        .Descending(x => x.DateTime),
                    new CreateIndexOptions { Background = true, Name = "idx_guid_datetime_desc" }),

                // TTL index: MongoDB auto-deletes documents older than TickerRetentionDays.
                // This prevents unbounded collection growth which degrades all queries over time.
                new CreateIndexModel<TradeOpportunityTickerModel>(
                    Builders<TradeOpportunityTickerModel>.IndexKeys.Ascending(x => x.DateTime),
                    new CreateIndexOptions
                    {
                        Background = true,
                        Name = "idx_datetime_ttl",
                        ExpireAfter = TimeSpan.FromDays(mongoSettings.TickerRetentionDays)
                    }),
            ]);
        }

        public Task InsertAsync(TradeOpportunityTickerModel ticker)
            => _collection.InsertOneAsync(ticker);

        public Task DeleteAllByGuidAsync(Guid guid)
            => _collection.DeleteManyAsync(
                Builders<TradeOpportunityTickerModel>.Filter.Eq(x => x.Guid, guid));

        public async Task<long> DeleteOrphanedAsync(IEnumerable<Guid> openGuids)
        {
            var result = await _collection.DeleteManyAsync(
                Builders<TradeOpportunityTickerModel>.Filter.Nin(x => x.Guid, openGuids));
            return result.DeletedCount;
        }

        public Task<TradeOpportunityTickerModel?> GetLatestByGuidAsync(Guid guid)
            => _collection
                .Find(Builders<TradeOpportunityTickerModel>.Filter.Eq(x => x.Guid, guid))
                .SortByDescending(x => x.DateTime)
                .Limit(1)
                .FirstOrDefaultAsync()!;

        public async Task<List<TradeOpportunityTickerModel>> GetRemainingWithoutOrderBookAsync(Guid guid, int skip, int limit)
        {
            // Using the typed collection + strongly-typed projection ensures the compound
            // index {Guid, DateTime DESC} is always used. The raw BsonDocument approach
            // previously risked a silent index miss if the BSON binary subtype didn't match.
            return await _collection
                .Find(Builders<TradeOpportunityTickerModel>.Filter.Eq(x => x.Guid, guid))
                .SortByDescending(x => x.DateTime)
                .Skip(skip)
                .Limit(limit)
                .Project<TradeOpportunityTickerModel>(WithoutOrderBook)
                .ToListAsync();
        }

        public async Task<List<TradeOpportunityTickerModel>> GetSinceWithoutOrderBookAsync(Guid guid, DateTime since, int limit)
        {
            var filter = Builders<TradeOpportunityTickerModel>.Filter.And(
                Builders<TradeOpportunityTickerModel>.Filter.Eq(x => x.Guid, guid),
                Builders<TradeOpportunityTickerModel>.Filter.Gt(x => x.DateTime, since));

            // Newest-first so the index {Guid, DateTime DESC} serves the sort and Limit keeps the live edge; flipped to
            // chronological order for the caller.
            var newestFirst = await _collection
                .Find(filter)
                .SortByDescending(x => x.DateTime)
                .Limit(limit)
                .Project<TradeOpportunityTickerModel>(WithoutOrderBook)
                .ToListAsync();

            newestFirst.Reverse();
            return newestFirst;
        }

        private static readonly ProjectionDefinition<TradeOpportunityTickerModel> WithoutOrderBook =
            Builders<TradeOpportunityTickerModel>.Projection
                .Exclude(x => x.BidsExchangeA)
                .Exclude(x => x.AsksExchangeA)
                .Exclude(x => x.BidsExchangeB)
                .Exclude(x => x.AsksExchangeB);
    }
}
