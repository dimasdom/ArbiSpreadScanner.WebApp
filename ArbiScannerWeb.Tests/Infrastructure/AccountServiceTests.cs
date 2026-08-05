using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.DbContext;
using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using StackExchange.Redis;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class AccountServiceTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly Mock<IDatabase> _redisDb;
    private readonly AppDbContext _dbContext;
    private readonly AccountService _sut;

    public AccountServiceTests()
    {
        Mock<IConnectionMultiplexer> redis;
        (redis, _redisDb) = MockHelpers.CreateRedisMocks();
        _redisDb.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _dbContext = MockHelpers.CreateInMemoryDbContext();

        _sut = new AccountService(_dbContext, _httpContextAccessor.Object, redis.Object);
    }

    private void SetupAuthenticatedUser(string userId)
    {
        var identity = new Mock<System.Security.Principal.IIdentity>();
        identity.Setup(i => i.Name).Returns(userId);
        var principal = new Mock<System.Security.Claims.ClaimsPrincipal>();
        principal.Setup(p => p.Identity).Returns(identity.Object);
        var ctx = new DefaultHttpContext();
        var wrapped = new Mock<HttpContext>();
        wrapped.Setup(c => c.User).Returns(principal.Object);
        wrapped.Setup(c => c.Request).Returns(ctx.Request);
        wrapped.Setup(c => c.Connection).Returns(ctx.Connection);
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(wrapped.Object);
    }

    private void SetupUnauthenticated() => _httpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

    [Fact]
    public async Task UpdateDetails_Unauthenticated_ReturnsFail()
    {
        SetupUnauthenticated();

        var result = await _sut.UpdateDetails(new AccountEditDto());

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateDetails_UserNotFound_ReturnsFail()
    {
        SetupAuthenticatedUser("missing");

        var result = await _sut.UpdateDetails(new AccountEditDto());

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateDetails_Success_UpdatesSettingsAndExchanges()
    {
        SetupAuthenticatedUser("acc1");
        var exchange = new ExchangeModel { Id = 1, Name = "binance" };
        _dbContext.Exchanges.Add(exchange);
        var settings = new UserSettingsModel { Id = 1, AccountId = "acc1" };
        var account = new AccountModel { Id = "acc1", UserSettingsId = 1, UserSettings = settings };
        _dbContext.Users.Add(account);
        await _dbContext.SaveChangesAsync();

        var editDto = new AccountEditDto
        {
            SpreadSize = 5,
            PositionSize = 20,
            FuturesSpread = true,
            Exchanges = new List<UserExchangeModel> { new() { Exchange = exchange } }
        };

        var result = await _sut.UpdateDetails(editDto);

        result.IsSuccess.Should().BeTrue();
        var reloaded = await _dbContext.UsersSettings.FindAsync(1);
        reloaded!.SpreadSize.Should().Be(5);
        reloaded.PositionSize.Should().Be(20);
        reloaded.FuturesSpread.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserData_CachedEntryPresent_ReturnsFromCache()
    {
        SetupAuthenticatedUser("u1");
        var cached = """{"UserSettings":{"AccountId":"u1"},"Email":"cached@b.com","EmailConfirmed":true}""";
        _redisDb.Setup(d => d.StringGetAsync((RedisKey)"userEntity:u1", It.IsAny<CommandFlags>())).ReturnsAsync((RedisValue)cached);

        var result = await _sut.GetUserData();

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("cached@b.com");
    }

    [Fact]
    public async Task GetUserData_NoCacheAndUnauthenticated_ReturnsFail()
    {
        SetupUnauthenticated();

        var result = await _sut.GetUserData();

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserData_NoCacheUserNotFound_ReturnsFail()
    {
        SetupAuthenticatedUser("missing");

        var result = await _sut.GetUserData();

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserData_NoCacheUserFound_ReturnsDtoAndCaches()
    {
        SetupAuthenticatedUser("acc1");
        var settings = new UserSettingsModel { Id = 5, AccountId = "acc1" };
        var account = new AccountModel { Id = "acc1", UserSettingsId = 5, Email = "acc1@b.com", UserSettings = settings };
        _dbContext.Users.Add(account);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetUserData();

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("acc1@b.com");
        _redisDb.Verify(d => d.StringSetAsync((RedisKey)"userEntity:acc1", It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()), Times.Once);
    }
}
