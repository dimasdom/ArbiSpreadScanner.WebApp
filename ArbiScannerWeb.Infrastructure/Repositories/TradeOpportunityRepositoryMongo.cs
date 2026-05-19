using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Settings;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Abstractions.Interfaces.Repositories
{
    public class TradeOpportunityRepositoryMongo : ITradeOpportunityRepository
    {
        private readonly IMongoCollection<TradeOpportunityModel> _collection;

        public TradeOpportunityRepositoryMongo(IMongoDatabase mongoDatabase, MongoDbSettings mongoSettings)
        {
            _collection = mongoDatabase.GetCollection<TradeOpportunityModel>(mongoSettings.CurrentSpreadsCollection);

            _collection.Indexes.CreateOne(
                new CreateIndexModel<TradeOpportunityModel>(
                    Builders<TradeOpportunityModel>.IndexKeys
                        .Ascending(x => x.OrderStatus)
                        .Ascending(x => x.Type),
                    new CreateIndexOptions { Background = true, Name = "idx_orderstatus_type" }));
        }

        public Task InsertAsync(TradeOpportunityModel model)
            => _collection.InsertOneAsync(model);

        public Task SetStatusClosedAsync(Guid guid)
        {
            var update = Builders<TradeOpportunityModel>.Update.Set(x => x.OrderStatus, OrderStatus.Closed);
            return _collection.UpdateOneAsync(
                Builders<TradeOpportunityModel>.Filter.Eq(x => x.Guid, guid),
                update);
        }

        public async Task<List<TradeOpportunityModel>> GetAllAsync()
            => await _collection.Find(Builders<TradeOpportunityModel>.Filter.Empty).ToListAsync();

        public Task<TradeOpportunityModel?> GetByGuidAsync(Guid guid)
            => _collection.Find(Builders<TradeOpportunityModel>.Filter.Eq(x => x.Guid, guid)).FirstOrDefaultAsync()!;

        public Task ReplaceAsync(TradeOpportunityModel model)
            => _collection.ReplaceOneAsync(
                Builders<TradeOpportunityModel>.Filter.Eq(x => x.Guid, model.Guid),
                model);

        public async Task<List<TradeOpportunityModel>> GetOpenByTypeAsync(SpreadType type)
            => await _collection.Find(
                Builders<TradeOpportunityModel>.Filter.And(
                    Builders<TradeOpportunityModel>.Filter.Eq(x => x.OrderStatus, OrderStatus.Open),
                    Builders<TradeOpportunityModel>.Filter.Eq(x => x.Type, type)))
                .ToListAsync();

        public async Task<List<TradeOpportunityModel>> GetByTypeAsync(SpreadType type)
            => await _collection.Find(
                Builders<TradeOpportunityModel>.Filter.Eq(x => x.Type, type))
                .ToListAsync();
    }
}
