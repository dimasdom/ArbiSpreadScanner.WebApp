using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Tests.Helpers;
using FluentAssertions;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Moq;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class SubscriptionServiceTests
{
    private readonly Mock<IAdminService> _adminService = new();
    private readonly Mock<IHttpContextAccessor> _httpContext = new();
    private readonly Mock<IConnectionMultiplexer> _redis;
    private readonly Mock<IDatabase> _redisDb;
    private readonly SubscriptionService _sut;

    private const string UserId = "user-42";

    public SubscriptionServiceTests()
    {
        (_redis, _redisDb) = MockHelpers.CreateRedisMocks();
        _sut = new SubscriptionService(_adminService.Object, _httpContext.Object, _redis.Object);
        SetupAuthenticatedUser(UserId);
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

    private void SetupUnauthenticated()
    {
        _httpContext.Setup(a => a.HttpContext).Returns((HttpContext?)null);
    }

    private void SetupRedisNull()
    {
        _redisDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
    }

    private void SetupRedisSubscription(UserSubscriptionModel sub)
    {
        _redisDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)JsonConvert.SerializeObject(sub));
    }

    [Fact]
    public async Task CheckIfUserHasActiveSubscription_ActiveSub_ReturnsTrue()
    {
        var sub = new UserSubscriptionModel { EndDate = DateTime.UtcNow.AddDays(10) };
        SetupRedisNull();
        _adminService.Setup(a => a.GetUserActiveSubscriptionsAsync(UserId))
            .ReturnsAsync(Result.Ok(sub));

        var ok = await _sut.CheckIfUserHasActiveSubscriptionAsync();

        ok.Should().BeTrue();
    }

    [Fact]
    public async Task CheckIfUserHasActiveSubscription_ExpiredSub_ReturnsFalse()
    {
        var sub = new UserSubscriptionModel { EndDate = DateTime.UtcNow.AddDays(-1) };
        SetupRedisNull();
        _adminService.Setup(a => a.GetUserActiveSubscriptionsAsync(UserId))
            .ReturnsAsync(Result.Ok(sub));

        var ok = await _sut.CheckIfUserHasActiveSubscriptionAsync();

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task CheckIfUserHasActiveSubscription_AdminServiceFails_ReturnsFalse()
    {
        SetupRedisNull();
        _adminService.Setup(a => a.GetUserActiveSubscriptionsAsync(UserId))
            .ReturnsAsync(Result.Fail<UserSubscriptionModel>("not found"));

        var ok = await _sut.CheckIfUserHasActiveSubscriptionAsync();

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserActiveSubscriptions_Unauthenticated_ReturnsFail()
    {
        SetupUnauthenticated();

        var result = await _sut.GetUserActiveSubscriptionsAsync();

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserActiveSubscriptions_RedisHit_ReturnsCachedSubWithoutCallingAdmin()
    {
        var cached = new UserSubscriptionModel { EndDate = DateTime.UtcNow.AddDays(5) };
        SetupRedisSubscription(cached);

        var result = await _sut.GetUserActiveSubscriptionsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        _adminService.Verify(a => a.GetUserActiveSubscriptionsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetUserActiveSubscriptions_RedisExpiredCache_CallsAdminAndReturnsDto()
    {
        var stale = new UserSubscriptionModel { EndDate = DateTime.UtcNow.AddDays(-1) };
        SetupRedisSubscription(stale);

        var fresh = new UserSubscriptionModel { EndDate = DateTime.UtcNow.AddDays(10) };
        _adminService.Setup(a => a.GetUserActiveSubscriptionsAsync(UserId))
            .ReturnsAsync(Result.Ok(fresh));
        _redisDb.Setup(d => d.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(), It.IsAny<bool>(),
            It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _sut.GetUserActiveSubscriptionsAsync();

        result.IsSuccess.Should().BeTrue();
        _adminService.Verify(a => a.GetUserActiveSubscriptionsAsync(UserId), Times.Once);
    }

    [Fact]
    public async Task GetUserActiveSubscriptions_NullAdminResult_ReturnsInactiveDto()
    {
        SetupRedisNull();
        _adminService.Setup(a => a.GetUserActiveSubscriptionsAsync(UserId))
            .ReturnsAsync(Result.Ok<UserSubscriptionModel>(null!));
        _redisDb.Setup(d => d.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(), It.IsAny<bool>(),
            It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _sut.GetUserActiveSubscriptionsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePayment_Unauthenticated_ReturnsFail()
    {
        SetupUnauthenticated();

        var result = await _sut.CreatePaymentAsync(1);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task CreatePayment_AdminServiceFails_ReturnsFail()
    {
        _adminService.Setup(a => a.CreatePaymentAsync(UserId, 1))
            .ReturnsAsync(Result.Fail<UserSubscriptionPayment>("bad request"));

        var result = await _sut.CreatePaymentAsync(1);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task CreatePayment_Success_ReturnsPayment()
    {
        var payment = new UserSubscriptionPayment { Id = 7 };
        _adminService.Setup(a => a.CreatePaymentAsync(UserId, 2))
            .ReturnsAsync(Result.Ok(payment));

        var result = await _sut.CreatePaymentAsync(2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(7);
    }

    [Fact]
    public async Task GetUserActivePayments_Unauthenticated_ReturnsFail()
    {
        SetupUnauthenticated();

        var result = await _sut.GetUserActivePaymentsAsync();

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserActivePayments_DelegatesToAdminService()
    {
        var payment = new UserSubscriptionPayment { Id = 3 };
        _adminService.Setup(a => a.GetUserActivePaymentsAsync(UserId))
            .ReturnsAsync(Result.Ok(payment));

        var result = await _sut.GetUserActivePaymentsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(3);
    }

    [Fact]
    public async Task GetAllSubscriptions_DelegatesToAdminAndReturnsList()
    {
        var list = new List<SubscriptionModel> { new() { Id = 1 }, new() { Id = 2 } };
        _adminService.Setup(a => a.GetAllSubscriptionsAsync()).ReturnsAsync(Result.Ok(list));

        var result = await _sut.GetAllSubscriptionsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }
}
