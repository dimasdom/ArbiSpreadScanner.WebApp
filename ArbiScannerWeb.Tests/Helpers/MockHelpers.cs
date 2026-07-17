using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.DbContext;
using ArbiScannerWeb.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using StackExchange.Redis;

namespace ArbiScannerWeb.Tests.Helpers;

internal static class MockHelpers
{
    internal static Mock<UserManager<AccountModel>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<AccountModel>>();
#pragma warning disable CS8625
        return new Mock<UserManager<AccountModel>>(
            store.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
    }

    internal static Mock<SignInManager<AccountModel>> CreateSignInManagerMock(
        Mock<UserManager<AccountModel>> userManagerMock)
    {
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<AccountModel>>();
#pragma warning disable CS8625
        return new Mock<SignInManager<AccountModel>>(
            userManagerMock.Object,
            contextAccessor.Object,
            claimsFactory.Object,
            null, null, null, null);
#pragma warning restore CS8625
    }

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

    internal static JwtOptions CreateTestJwtOptions() => new()
    {
        SigningKey = "test-signing-key-that-is-at-least-32-chars-long",
        Issuer = "test-issuer",
        Audience = "test-audience",
        AccessTokenExpirationMinutes = 15,
        RefreshTokenExpirationDays = 7
    };
}
