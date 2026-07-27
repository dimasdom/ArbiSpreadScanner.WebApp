using System.Net;
using System.Net.Http;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class AdminServiceTests
{
    private sealed class StubHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<HttpRequestMessage> Requests { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage>? OnRequest { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (OnRequest is not null)
                return Task.FromResult(OnRequest(request));
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private readonly Mock<IConnectionMultiplexer> _redis;
    private readonly Mock<IDatabase> _redisDb;
    private readonly Mock<ILogger<AdminService>> _logger = new();
    private readonly StubHttpMessageHandler _handler = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
    private readonly IConfiguration _configuration;

    public AdminServiceTests()
    {
        (_redis, _redisDb) = MockHelpers.CreateRedisMocks();
        _httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(() => new HttpClient(_handler));
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminApiUrl"] = "http://admin.local",
            ["AdminUser:UserName"] = "admin",
            ["AdminUser:Password"] = "secret"
        }).Build();
    }

    private AdminService CreateSut() => new(_configuration, _redis.Object, _httpClientFactory.Object, _logger.Object);

    private void SetupCachedToken(string? token)
    {
        _redisDb.Setup(d => d.StringGetAsync("admin_service:jwt_token", It.IsAny<CommandFlags>()))
            .ReturnsAsync(token is null ? RedisValue.Null : token);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    // Mirrors AdminPanel's real Authenticate response: token is blanked from the body and
    // returned only via the adminpanel.access_token cookie (see AccountController.AppendAuthCookies).
    private static HttpResponseMessage AuthenticateResponse(string token)
    {
        var response = JsonResponse(HttpStatusCode.OK, """{"value":{"token":""}}""");
        response.Headers.Add("Set-Cookie", $"adminpanel.access_token={token}; path=/; httponly");
        return response;
    }

    [Fact]
    public void Constructor_MissingAdminApiUrl_Throws()
    {
        var config = new ConfigurationBuilder().Build();

        var act = () => new AdminService(config, _redis.Object, _httpClientFactory.Object, _logger.Object);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task GetAllSubscriptionsAsync_CachedToken_UsesCacheAndSucceeds()
    {
        SetupCachedToken("cached-token");
        _handler.OnRequest = req =>
        {
            req.Headers.Authorization!.Parameter.Should().Be("cached-token");
            return JsonResponse(HttpStatusCode.OK, """{"value":[{"Id":1},{"Id":2}]}""");
        };

        var result = await CreateSut().GetAllSubscriptionsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllSubscriptionsAsync_NoCachedToken_RefreshesThenSucceeds()
    {
        SetupCachedToken(null);
        _redisDb.Setup(d => d.StringSetAsync("admin_service:jwt_token", "fresh-token", It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        var callCount = 0;
        _handler.OnRequest = req =>
        {
            callCount++;
            if (req.RequestUri!.AbsolutePath.Contains("Authenticate"))
                return AuthenticateResponse("fresh-token");
            return JsonResponse(HttpStatusCode.OK, """{"value":[{"Id":1}]}""");
        };

        var result = await CreateSut().GetAllSubscriptionsAsync();

        result.IsSuccess.Should().BeTrue();
        callCount.Should().Be(2);
        _redisDb.Verify(d => d.StringSetAsync("admin_service:jwt_token", "fresh-token", It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task GetAllSubscriptionsAsync_AuthenticateFails_ProceedsWithoutTokenAndGetsUnauthorized()
    {
        SetupCachedToken(null);
        _handler.OnRequest = req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("Authenticate"))
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        };

        var result = await CreateSut().GetAllSubscriptionsAsync();

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllSubscriptionsAsync_AuthenticateResponseMissingToken_DoesNotSetHeader()
    {
        SetupCachedToken(null);
        _handler.OnRequest = req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("Authenticate"))
                return JsonResponse(HttpStatusCode.OK, """{"value":{}}""");
            return JsonResponse(HttpStatusCode.OK, """{"value":[]}""");
        };

        var result = await CreateSut().GetAllSubscriptionsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllSubscriptionsAsync_AuthenticateThrows_LogsErrorAndContinues()
    {
        SetupCachedToken(null);
        _handler.OnRequest = req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("Authenticate"))
                throw new HttpRequestException("network down");
            return JsonResponse(HttpStatusCode.OK, """{"value":[]}""");
        };

        var result = await CreateSut().GetAllSubscriptionsAsync();

        result.IsSuccess.Should().BeTrue();
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllSubscriptionsAsync_DoubleCheckedCacheHitAfterLock_SkipsHttpAuthenticate()
    {
        var calls = 0;
        _redisDb.Setup(d => d.StringGetAsync("admin_service:jwt_token", It.IsAny<CommandFlags>()))
            .ReturnsAsync(() => ++calls == 1 ? RedisValue.Null : (RedisValue)"already-refreshed");
        _handler.OnRequest = req =>
        {
            req.RequestUri!.AbsolutePath.Should().NotContain("Authenticate");
            return JsonResponse(HttpStatusCode.OK, """{"value":[]}""");
        };

        var result = await CreateSut().GetAllSubscriptionsAsync();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetPaymentStatusAsync_Unauthorized_RefreshesTokenAndRetries()
    {
        SetupCachedToken("stale-token");
        _redisDb.Setup(d => d.KeyDeleteAsync("admin_service:jwt_token", It.IsAny<CommandFlags>())).ReturnsAsync(true);
        _redisDb.Setup(d => d.StringSetAsync("admin_service:jwt_token", "new-token", It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        var getCalls = 0;
        _handler.OnRequest = req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("Authenticate"))
                return AuthenticateResponse("new-token");
            getCalls++;
            return getCalls == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : JsonResponse(HttpStatusCode.OK, """{"value":{"Id":9}}""");
        };

        var result = await CreateSut().GetPaymentStatusAsync(9);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(9);
        _redisDb.Verify(d => d.KeyDeleteAsync("admin_service:jwt_token", It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task GetSubscriptionDetailsAsync_NonSuccessStatus_ReturnsHttpErrorMessage()
    {
        SetupCachedToken("token");
        _handler.OnRequest = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        var result = await CreateSut().GetSubscriptionDetailsAsync(1);

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("NotFound");
    }

    [Fact]
    public async Task GetSubscriptionDetailsAsync_ResponseWithoutValueWrapper_DeserializesDirectly()
    {
        SetupCachedToken("token");
        _handler.OnRequest = _ => JsonResponse(HttpStatusCode.OK, """{"Id":42}""");

        var result = await CreateSut().GetSubscriptionDetailsAsync(1);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(42);
    }

    [Fact]
    public async Task GetUserActivePaymentsAsync_MalformedJson_ReturnsFailAndLogs()
    {
        SetupCachedToken("token");
        _handler.OnRequest = _ => JsonResponse(HttpStatusCode.OK, "not-json");

        var result = await CreateSut().GetUserActivePaymentsAsync("u1");

        result.IsFailed.Should().BeTrue();
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserActiveSubscriptionsAsync_Success_ReturnsModel()
    {
        SetupCachedToken("token");
        _handler.OnRequest = _ => JsonResponse(HttpStatusCode.OK, """{"value":{"UserId":"acc1"}}""");

        var result = await CreateSut().GetUserActiveSubscriptionsAsync("acc1");

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be("acc1");
    }

    [Fact]
    public async Task CreatePaymentAsync_Success_ReturnsPayment()
    {
        SetupCachedToken("token");
        _handler.OnRequest = _ => JsonResponse(HttpStatusCode.OK, """{"Value":{"Id":5}}""");

        var result = await CreateSut().CreatePaymentAsync("u1", 3);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreatePaymentAsync_NonSuccessStatus_ReturnsFail()
    {
        SetupCachedToken("token");
        _handler.OnRequest = _ => new HttpResponseMessage(HttpStatusCode.BadRequest);

        var result = await CreateSut().CreatePaymentAsync("u1", 3);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task CreatePaymentAsync_NullDeserializedBody_ReturnsFailedToDeserialize()
    {
        SetupCachedToken("token");
        _handler.OnRequest = _ => JsonResponse(HttpStatusCode.OK, "null");

        var result = await CreateSut().CreatePaymentAsync("u1", 3);

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("Failed to deserialize");
    }

    [Fact]
    public async Task CreatePaymentAsync_HttpThrows_ReturnsFailAndLogs()
    {
        SetupCachedToken("token");
        _handler.OnRequest = _ => throw new HttpRequestException("boom");

        var result = await CreateSut().CreatePaymentAsync("u1", 3);

        result.IsFailed.Should().BeTrue();
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CreatePaymentAsync_Unauthorized_RefreshesAndRetries()
    {
        SetupCachedToken("stale");
        _redisDb.Setup(d => d.KeyDeleteAsync("admin_service:jwt_token", It.IsAny<CommandFlags>())).ReturnsAsync(true);
        _redisDb.Setup(d => d.StringSetAsync("admin_service:jwt_token", "new-token", It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        var postCalls = 0;
        _handler.OnRequest = req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("Authenticate"))
                return AuthenticateResponse("new-token");
            postCalls++;
            return postCalls == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : JsonResponse(HttpStatusCode.OK, """{"Value":{"Id":11}}""");
        };

        var result = await CreateSut().CreatePaymentAsync("u1", 3);

        result.IsSuccess.Should().BeTrue();
        postCalls.Should().Be(2);
    }

    [Fact]
    public async Task CancelPayment_Success_ReturnsOk()
    {
        SetupCachedToken("token");
        _handler.OnRequest = _ => JsonResponse(HttpStatusCode.OK, """{"Value":{}}""");

        var result = await CreateSut().CancelPayment(7);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CancelPayment_Fails_ReturnsFail()
    {
        SetupCachedToken("token");
        _handler.OnRequest = _ => new HttpResponseMessage(HttpStatusCode.BadRequest);

        var result = await CreateSut().CancelPayment(7);

        result.IsFailed.Should().BeTrue();
    }
}
