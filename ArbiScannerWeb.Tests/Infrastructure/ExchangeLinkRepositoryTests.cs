using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.DbContext;
using ArbiScannerWeb.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class ExchangeLinkRepositoryTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly Mock<IDbContextFactory<AppDbContext>> _factory = new();
    private readonly ExchangeLinkRepository _sut;

    public ExchangeLinkRepositoryTests()
    {
        _factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateContext());
        _sut = new ExchangeLinkRepository(_factory.Object);
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new AppDbContext(options);
    }

    private async Task Seed(params ExchangeLinkModel[] links)
    {
        using var context = CreateContext();
        context.ExchangeLinks.AddRange(links);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsLinksOrderedByExchange()
    {
        await Seed(
            new ExchangeLinkModel { Exchange = "okx" },
            new ExchangeLinkModel { Exchange = "binance" });

        var result = await _sut.GetAllAsync();

        result.Should().HaveCount(2);
        result[0].Exchange.Should().Be("binance");
        result[1].Exchange.Should().Be("okx");
    }

    [Fact]
    public async Task GetAllAsync_NoLinks_ReturnsEmptyList()
    {
        var result = await _sut.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByExchangeNameAsync_CaseInsensitiveMatch_ReturnsLink()
    {
        await Seed(new ExchangeLinkModel { Exchange = "Binance" });

        var result = await _sut.GetByExchangeNameAsync("BINANCE");

        result.Should().NotBeNull();
        result!.Exchange.Should().Be("Binance");
    }

    [Fact]
    public async Task GetByExchangeNameAsync_NoMatch_ReturnsNull()
    {
        await Seed(new ExchangeLinkModel { Exchange = "Binance" });

        var result = await _sut.GetByExchangeNameAsync("kraken");

        result.Should().BeNull();
    }
}
