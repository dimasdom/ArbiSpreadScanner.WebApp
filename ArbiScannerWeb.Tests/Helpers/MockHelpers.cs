using ArbiScannerWeb.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Moq;
using StackExchange.Redis;

namespace ArbiScannerWeb.Tests.Helpers;

internal static class MockHelpers
{
    internal static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    internal static (Mock<IConnectionMultiplexer> Multiplexer, Mock<IDatabase> Database) CreateRedisMocks()
    {
        var mockDb = new Mock<IDatabase>();
        mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var mockMultiplexer = new Mock<IConnectionMultiplexer>();
        mockMultiplexer
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDb.Object);

        return (mockMultiplexer, mockDb);
    }
}
