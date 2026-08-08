using System.Net;
using System.Net.Http;
using ArbiScannerWeb.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class McpTokenServiceTests
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

    private readonly StubHttpMessageHandler _handler = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
    private readonly Mock<ILogger<McpTokenService>> _logger = new();
    private readonly IConfiguration _configuration;

    public McpTokenServiceTests()
    {
        _httpClientFactory.Setup(f => f.CreateClient("KeycloakMcpExchange"))
            .Returns(() => new HttpClient(_handler) { BaseAddress = new Uri("http://keycloak.local/realms/arbiscanner-web/") });
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Keycloak:McpExchange:ClientId"] = "arbiscanner-mcp",
            ["Keycloak:McpExchange:ClientSecret"] = "test-secret"
        }).Build();
    }

    private static IHttpContextAccessor CreateContextAccessor(string? authorizationHeader)
    {
        var context = new DefaultHttpContext();
        if (authorizationHeader is not null)
            context.Request.Headers.Authorization = new StringValues(authorizationHeader);

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(context);
        return accessor.Object;
    }

    private McpTokenService CreateSut(string? authorizationHeader = "Bearer caller-access-token") =>
        new(_httpClientFactory.Object, CreateContextAccessor(authorizationHeader), _configuration, _logger.Object);

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task GenerateAccessTokenAsync_NoAuthorizationHeader_ReturnsUnauthorized()
    {
        var result = await CreateSut(authorizationHeader: null).GenerateAccessTokenAsync(CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("bearer token");
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_NonBearerAuthorizationHeader_ReturnsUnauthorized()
    {
        var result = await CreateSut(authorizationHeader: "Basic dXNlcjpwYXNz").GenerateAccessTokenAsync(CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_Success_SendsTokenExchangeRequestAndReturnsAccessToken()
    {
        _handler.OnRequest = req =>
        {
            req.RequestUri!.AbsolutePath.Should().Be("/realms/arbiscanner-web/protocol/openid-connect/token");
            var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            body.Should().Contain("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Atoken-exchange");
            body.Should().Contain("subject_token=caller-access-token");
            body.Should().Contain("subject_token_type=urn%3Aietf%3Aparams%3Aoauth%3Atoken-type%3Aaccess_token");
            body.Should().NotContain("requested_token_type");
            body.Should().Contain("scope=openid+offline_access");
            body.Should().Contain("client_id=arbiscanner-mcp");
            body.Should().Contain("client_secret=test-secret");
            return JsonResponse(HttpStatusCode.OK, """{"access_token":"long-lived-token-value","expires_in":2592000,"token_type":"Bearer"}""");
        };

        var result = await CreateSut().GenerateAccessTokenAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("long-lived-token-value");
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_KeycloakReturnsNonSuccess_ReturnsFail()
    {
        _handler.OnRequest = _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("invalid_grant")
        };

        var result = await CreateSut().GenerateAccessTokenAsync(CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_ResponseMissingAccessToken_ReturnsFail()
    {
        _handler.OnRequest = _ => JsonResponse(HttpStatusCode.OK, """{"token_type":"Bearer"}""");

        var result = await CreateSut().GenerateAccessTokenAsync(CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_HttpThrows_ReturnsFailAndLogs()
    {
        _handler.OnRequest = _ => throw new HttpRequestException("network down");

        var result = await CreateSut().GenerateAccessTokenAsync(CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
