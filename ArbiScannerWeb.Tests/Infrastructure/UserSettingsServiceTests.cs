using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.DbContext;
using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using StackExchange.Redis;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class UserSettingsServiceTests
{
    private readonly AppDbContext _dbContext = MockHelpers.CreateInMemoryDbContext();
    private readonly Mock<IHttpContextAccessor> _httpContext = new();
    private readonly Mock<IConnectionMultiplexer> _redis;
    private readonly Mock<IDatabase> _redisDb;
    private readonly UserSettingsService _sut;

    public UserSettingsServiceTests()
    {
        (_redis, _redisDb) = MockHelpers.CreateRedisMocks();
        _redisDb.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
        _sut = new UserSettingsService(_dbContext, _httpContext.Object, _redis.Object);
    }

    private void SetupAuthenticatedUser(string userId)
    {
        var identity = new Mock<System.Security.Principal.IIdentity>();
        identity.Setup(i => i.Name).Returns(userId);
        var principal = new Mock<System.Security.Claims.ClaimsPrincipal>();
        principal.Setup(p => p.Identity).Returns(identity.Object);
        var ctx = new Mock<HttpContext>();
        ctx.Setup(c => c.User).Returns(principal.Object);
        _httpContext.Setup(a => a.HttpContext).Returns(ctx.Object);
    }

    private void SetupUnauthenticated() => _httpContext.Setup(a => a.HttpContext).Returns((HttpContext?)null);

    private async Task<AccountModel> SeedAccountWithSettings(string accountId, int settingsId)
    {
        var settings = new UserSettingsModel { Id = settingsId, AccountId = accountId };
        var account = new AccountModel { Id = accountId, UserSettingsId = settingsId, UserSettings = settings };
        _dbContext.Users.Add(account);
        await _dbContext.SaveChangesAsync();
        return account;
    }

    [Fact]
    public async Task CreateLinkRequestAsync_AccountNotFound_ReturnsFail()
    {
        var result = await _sut.CreateLinkRequestAsync("missing");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task CreateLinkRequestAsync_TelegramSettingsNotFound_ReturnsFail()
    {
        var account = new AccountModel { Id = "acc1", UserSettingsId = 999, UserSettings = null! };
        _dbContext.Users.Add(account);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.CreateLinkRequestAsync("acc1");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task CreateLinkRequestAsync_Success_CreatesLinkAndInvalidatesCache()
    {
        await SeedAccountWithSettings("acc1", 1);

        var result = await _sut.CreateLinkRequestAsync("acc1");

        result.IsSuccess.Should().BeTrue();
        result.Value.AccountId.Should().Be("acc1");
        _dbContext.TelegramLinkRequests.Should().ContainSingle(t => t.AccountId == "acc1");
        _redisDb.Verify(d => d.KeyDeleteAsync((RedisKey)"userEntity:acc1", It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task CreateLinkRequestAsyncForAuthUser_Unauthenticated_ReturnsFail()
    {
        SetupUnauthenticated();

        var result = await _sut.CreateLinkRequestAsyncForAuthUser();

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task CreateLinkRequestAsyncForAuthUser_Authenticated_DelegatesToAccountId()
    {
        await SeedAccountWithSettings("acc1", 1);
        SetupAuthenticatedUser("acc1");

        var result = await _sut.CreateLinkRequestAsyncForAuthUser();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserSettingsByAccountIdAsync_AccountNotFound_ReturnsFail()
    {
        var result = await _sut.GetUserSettingsByAccountIdAsync("missing");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserSettingsByAccountIdAsync_Found_ReturnsSettings()
    {
        await SeedAccountWithSettings("acc1", 1);

        var result = await _sut.GetUserSettingsByAccountIdAsync("acc1");

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccountId.Should().Be("acc1");
    }

    [Fact]
    public async Task RemoveTelegramLinkAsync_AccountNotFound_ReturnsFail()
    {
        var result = await _sut.RemoveTelegramLinkAsync("missing");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveTelegramLinkAsync_Success_RemovesLinksAndResetsChatId()
    {
        var account = await SeedAccountWithSettings("acc1", 1);
        _dbContext.TelegramLinkRequests.Add(new TelegramLinkRequest { AccountId = "acc1" });
        await _dbContext.SaveChangesAsync();
        var settings = await _dbContext.UsersSettings.FindAsync(account.UserSettingsId);
        settings!.ChatId = 555;
        settings.UserName = "tguser";
        await _dbContext.SaveChangesAsync();

        var result = await _sut.RemoveTelegramLinkAsync("acc1");

        result.IsSuccess.Should().BeTrue();
        _dbContext.TelegramLinkRequests.Should().NotContain(t => t.AccountId == "acc1");
        var reloaded = await _dbContext.UsersSettings.FindAsync(account.UserSettingsId);
        reloaded!.ChatId.Should().Be(0);
        reloaded.UserName.Should().BeNull();
        _redisDb.Verify(d => d.KeyDeleteAsync((RedisKey)"userEntity:acc1", It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RemoveTelegramLinkAsyncForAuthUser_Unauthenticated_ReturnsFail()
    {
        SetupUnauthenticated();

        var result = await _sut.RemoveTelegramLinkAsyncForAuthUser();

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveTelegramLinkAsyncForAuthUser_Authenticated_DelegatesToAccountId()
    {
        await SeedAccountWithSettings("acc1", 1);
        SetupAuthenticatedUser("acc1");

        var result = await _sut.RemoveTelegramLinkAsyncForAuthUser();

        result.IsSuccess.Should().BeTrue();
    }
}
