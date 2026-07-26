using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Settings;
using FluentAssertions;
using MongoDB.Driver;
using Moq;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class TradeOpportunityRepositoryMongoTests
{
    private readonly Mock<IMongoCollection<TradeOpportunityModel>> _collection = new();
    private readonly TradeOpportunityRepositoryMongo _sut;

    public TradeOpportunityRepositoryMongoTests()
    {
        var indexManager = new Mock<IMongoIndexManager<TradeOpportunityModel>>();
        _collection.Setup(c => c.Indexes).Returns(indexManager.Object);

        var database = new Mock<IMongoDatabase>();
        database.Setup(d => d.GetCollection<TradeOpportunityModel>("CurrentSpreads", null)).Returns(_collection.Object);

        _sut = new TradeOpportunityRepositoryMongo(database.Object, new MongoDbSettings());
    }

    private static Mock<IAsyncCursor<TradeOpportunityModel>> CreateCursor(List<TradeOpportunityModel> items)
    {
        var cursor = new Mock<IAsyncCursor<TradeOpportunityModel>>();
        cursor.Setup(c => c.Current).Returns(items);
        cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true).ReturnsAsync(false);
        return cursor;
    }

    private void SetupFind(List<TradeOpportunityModel> items)
    {
        var cursor = CreateCursor(items);
        _collection.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<TradeOpportunityModel>>(),
                It.IsAny<FindOptions<TradeOpportunityModel, TradeOpportunityModel>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);
    }

    private static TradeOpportunityModel BuildModel(Guid guid) => new()
    {
        Guid = guid,
        Symbol = "BTC/USDT",
        ExchangeRateA = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "binance" },
        ExchangeRateB = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "okx" },
        ExchangeShort = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "binance" },
        ExchangeLong = new ExchangeRateModel { Symbol = "BTC/USDT", Exchange = "okx" }
    };

    [Fact]
    public void Constructor_CreatesIndexOnOrderStatusAndType()
    {
        var indexManager = new Mock<IMongoIndexManager<TradeOpportunityModel>>();
        var collection = new Mock<IMongoCollection<TradeOpportunityModel>>();
        collection.Setup(c => c.Indexes).Returns(indexManager.Object);
        var database = new Mock<IMongoDatabase>();
        database.Setup(d => d.GetCollection<TradeOpportunityModel>("CurrentSpreads", null)).Returns(collection.Object);

        _ = new TradeOpportunityRepositoryMongo(database.Object, new MongoDbSettings());

        indexManager.Verify(i => i.CreateOne(
            It.IsAny<CreateIndexModel<TradeOpportunityModel>>(),
            It.IsAny<CreateOneIndexOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpsertAsync_CallsReplaceOneAsyncWithUpsertOption()
    {
        var model = BuildModel(Guid.NewGuid());
        _collection.Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<TradeOpportunityModel>>(), model,
                It.Is<ReplaceOptions>(o => o.IsUpsert), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReplaceOneResult?)null!);

        await _sut.UpsertAsync(model);

        _collection.Verify(c => c.ReplaceOneAsync(
            It.IsAny<FilterDefinition<TradeOpportunityModel>>(), model,
            It.Is<ReplaceOptions>(o => o.IsUpsert), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetStatusClosedAsync_CallsUpdateOneAsync()
    {
        var guid = Guid.NewGuid();
        _collection.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<TradeOpportunityModel>>(),
                It.IsAny<UpdateDefinition<TradeOpportunityModel>>(),
                (UpdateOptions?)null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdateResult?)null!);

        await _sut.SetStatusClosedAsync(guid);

        _collection.Verify(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<TradeOpportunityModel>>(),
            It.IsAny<UpdateDefinition<TradeOpportunityModel>>(),
            (UpdateOptions?)null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllDocuments()
    {
        var models = new List<TradeOpportunityModel> { BuildModel(Guid.NewGuid()), BuildModel(Guid.NewGuid()) };
        SetupFind(models);

        var result = await _sut.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByGuidAsync_Found_ReturnsModel()
    {
        var guid = Guid.NewGuid();
        SetupFind(new List<TradeOpportunityModel> { BuildModel(guid) });

        var result = await _sut.GetByGuidAsync(guid);

        result!.Guid.Should().Be(guid);
    }

    [Fact]
    public async Task GetByGuidAsync_NotFound_ReturnsNull()
    {
        SetupFind(new List<TradeOpportunityModel>());

        var result = await _sut.GetByGuidAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task ReplaceAsync_CallsReplaceOneAsyncWithoutUpsert()
    {
        var model = BuildModel(Guid.NewGuid());
        _collection.Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<TradeOpportunityModel>>(), model,
                (ReplaceOptions?)null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReplaceOneResult?)null!);

        await _sut.ReplaceAsync(model);

        _collection.Verify(c => c.ReplaceOneAsync(
            It.IsAny<FilterDefinition<TradeOpportunityModel>>(), model,
            (ReplaceOptions?)null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOpenByTypeAsync_ReturnsMatchingDocuments()
    {
        var models = new List<TradeOpportunityModel> { BuildModel(Guid.NewGuid()) };
        SetupFind(models);

        var result = await _sut.GetOpenByTypeAsync(SpreadType.Futures);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByTypeAsync_ReturnsMatchingDocuments()
    {
        var models = new List<TradeOpportunityModel> { BuildModel(Guid.NewGuid()) };
        SetupFind(models);

        var result = await _sut.GetByTypeAsync(SpreadType.Spot);

        result.Should().HaveCount(1);
    }
}
