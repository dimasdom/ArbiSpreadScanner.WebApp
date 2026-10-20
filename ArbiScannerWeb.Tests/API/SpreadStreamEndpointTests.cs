using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.API.Controllers;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Realtime;
using ArbiScannerWeb.Tests.Infrastructure.Realtime;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ArbiScannerWeb.Tests.API;

/// <summary>
/// The real HTTP pipeline (routing, auth, controller, streaming response) over an in-process TestServer, with the real broker,
/// notifier and stream service; only the databases are mocked. Needs no Docker.
/// </summary>
public sealed class SpreadStreamEndpointTests : IAsyncLifetime
{
    private static readonly DateTime T0 = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Mock<ISubscriptionService> _subscriptions = new();
    private readonly Mock<ITradeOpportunityRepository> _spreads = new();
    private readonly Mock<ITradeOpportunityTickerRepository> _tickers = new();
    private readonly Guid _spreadId = Guid.NewGuid();
    private readonly Dictionary<string, string?> _config = new()
    {
        ["SpreadStream:KeepAliveSeconds"] = "30",
        ["SpreadStream:MaxLifetimeMinutes"] = "30",
        ["SpreadStream:SubscriberBufferSize"] = "8",
    };

    private WebApplication _app = default!;
    private HttpClient _client = default!;

    private string StreamUrl => $"/api/TradeOpportunity/Stream/{_spreadId}";

    public async Task InitializeAsync()
    {
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(true);
        _spreads.Setup(r => r.GetByGuidAsync(_spreadId)).ReturnsAsync(TestSpreads.Build(_spreadId));
        _tickers.Setup(r => r.GetSinceWithoutOrderBookAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<int>())).ReturnsAsync([]);

        await StartAsync();
    }

    private async Task StartAsync()
    {
        if (_app is not null) await DisposeAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(_config);
        builder.Services.AddSingleton(_subscriptions.Object);
        builder.Services.AddSingleton(_spreads.Object);
        builder.Services.AddSingleton(_tickers.Object);
        builder.Services.AddSpreadStream(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(SpreadStreamController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        await _app.StartAsync();

        _client = _app.GetTestClient();
        _client.Timeout = Timeout;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
    }

    private IRealtimeNotifier Notifier => _app.Services.GetRequiredService<IRealtimeNotifier>();

    private Task PublishTickerAsync(int seconds, bool withOrderBook = true) =>
        Notifier.NotifyGroupAsync(_spreadId.ToString(), new MessageDto { Ticker = BuildTicker(seconds, withOrderBook) });

    private static TradeOpportunityTickerModel BuildTicker(int seconds, bool withOrderBook = true) => new()
    {
        Guid = Guid.Empty,
        Symbol = "BTC/USDT",
        Spread = seconds,
        EchangeA = "Binance",
        ExchangeB = "Bybit",
        DateTime = T0.AddSeconds(seconds),
        BidsExchangeA = withOrderBook ? [new OrderLevelModel { Price = 100, Amount = 1 }] : null,
    };

    private static long IdOf(int seconds) => new DateTimeOffset(T0.AddSeconds(seconds)).ToUnixTimeMilliseconds();

    private async Task<(HttpResponseMessage Response, SseReader Reader)> OpenStreamAsync(Action<HttpRequestMessage>? configure = null, string user = "user-1")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, StreamUrl);
        request.Headers.Add("X-Test-User", user);
        configure?.Invoke(request);

        var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        return (response, new SseReader(await response.Content.ReadAsStreamAsync()));
    }

    private async Task<(HttpResponseMessage Response, SseReader Reader)> OpenAndAwaitPreambleAsync(Action<HttpRequestMessage>? configure = null)
    {
        var opened = await OpenStreamAsync(configure);
        (await opened.Reader.NextAsync()).Comment.Should().Be("connected");
        return opened;
    }

    [Fact]
    public async Task WithoutCredentials_Returns401()
    {
        var response = await _client.GetAsync(StreamUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WithoutAnActiveSubscription_Returns403_AndNeverTouchesTheSpreadStore()
    {
        _subscriptions.Setup(s => s.CheckIfUserHasActiveSubscriptionAsync()).ReturnsAsync(false);

        var (response, _) = await OpenStreamAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _spreads.Verify(r => r.GetByGuidAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task UnknownSpread_Returns404()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/TradeOpportunity/Stream/{Guid.NewGuid()}");
        request.Headers.Add("X-Test-User", "user-1");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonGuidId_Returns404()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/TradeOpportunity/Stream/not-a-guid");
        request.Headers.Add("X-Test-User", "user-1");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OpensAnEventStream_AndFlushesThePreambleBeforeAnyEvent()
    {
        var (response, reader) = await OpenStreamAsync();
        using var _ = response;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        response.Headers.CacheControl!.NoCache.Should().BeTrue();
        response.Headers.GetValues("X-Accel-Buffering").Should().ContainSingle("no");

        var first = await reader.NextAsync();
        first.Comment.Should().Be("connected");
        first.Retry.Should().Be(3000);
    }

    [Fact]
    public async Task PublishedTickers_ArriveAsTickerEventsWithIdsAndFullOrderBooks()
    {
        var (response, reader) = await OpenAndAwaitPreambleAsync();
        using var _ = response;

        await PublishTickerAsync(1);
        await PublishTickerAsync(2);

        var first = await reader.NextAsync();
        first.Event.Should().Be("ticker");
        first.Id.Should().Be(IdOf(1).ToString());
        using var json = JsonDocument.Parse(first.Data!);
        json.RootElement.GetProperty("symbol").GetString().Should().Be("BTC/USDT");
        json.RootElement.GetProperty("echangeA").GetString().Should().Be("Binance");
        json.RootElement.GetProperty("bidsExchangeA")[0].GetProperty("price").GetDouble().Should().Be(100);

        (await reader.NextAsync()).Id.Should().Be(IdOf(2).ToString());
    }

    [Fact]
    public async Task OtherSpreadsTickers_AreNotDelivered()
    {
        var (response, reader) = await OpenAndAwaitPreambleAsync();
        using var _ = response;

        await Notifier.NotifyGroupAsync(Guid.NewGuid().ToString(), new MessageDto { Ticker = BuildTicker(1) });
        await PublishTickerAsync(2);

        (await reader.NextAsync()).Id.Should().Be(IdOf(2).ToString());
    }

    [Fact]
    public async Task ClosingTheSpread_SendsCloseAndEndsTheResponse()
    {
        var (response, reader) = await OpenAndAwaitPreambleAsync();
        using var _ = response;

        await Notifier.NotifyGroupAsync(_spreadId.ToString(), new MessageDto { TradeOpportunity = TestSpreads.Build(_spreadId, action: MarketPositionAction.Close) });

        var close = await reader.NextAsync();
        close.Event.Should().Be("close");
        close.Id.Should().BeNull();
        JsonDocument.Parse(close.Data!).RootElement.GetProperty("spreadId").GetGuid().Should().Be(_spreadId);
        (await reader.NextAsync()).IsEndOfStream.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectingToAnAlreadyClosedSpread_ImmediatelySendsClose()
    {
        _spreads.Setup(r => r.GetByGuidAsync(_spreadId)).ReturnsAsync(TestSpreads.Build(_spreadId, OrderStatus.Closed));

        var (response, reader) = await OpenAndAwaitPreambleAsync();
        using var _ = response;

        (await reader.NextAsync()).Event.Should().Be("close");
        (await reader.NextAsync()).IsEndOfStream.Should().BeTrue();
    }

    [Fact]
    public async Task LastEventId_ReplaysMissedTickersWithoutOrderBooks_ThenContinuesLive()
    {
        _tickers
            .Setup(r => r.GetSinceWithoutOrderBookAsync(_spreadId, T0.AddSeconds(1), It.IsAny<int>()))
            .ReturnsAsync([BuildTicker(2, withOrderBook: false), BuildTicker(3, withOrderBook: false)]);

        var (response, reader) = await OpenAndAwaitPreambleAsync(r => r.Headers.Add("Last-Event-ID", IdOf(1).ToString()));
        using var _ = response;
        await PublishTickerAsync(4);

        var replayed = await reader.NextAsync();
        replayed.Id.Should().Be(IdOf(2).ToString());
        JsonDocument.Parse(replayed.Data!).RootElement.TryGetProperty("bidsExchangeA", out var book).Should().BeTrue();
        book.ValueKind.Should().Be(JsonValueKind.Null);
        (await reader.NextAsync()).Id.Should().Be(IdOf(3).ToString());
        (await reader.NextAsync()).Id.Should().Be(IdOf(4).ToString());
    }

    [Fact]
    public async Task InvalidLastEventId_IsIgnored_NothingIsReplayed()
    {
        var (response, reader) = await OpenAndAwaitPreambleAsync(r => r.Headers.Add("Last-Event-ID", "garbage"));
        using var _ = response;

        await PublishTickerAsync(1);

        (await reader.NextAsync()).Id.Should().Be(IdOf(1).ToString());
        _tickers.Verify(r => r.GetSinceWithoutOrderBookAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task WhenIdle_SendsKeepAliveComments()
    {
        _config["SpreadStream:KeepAliveSeconds"] = "0.05";
        await StartAsync();

        var (response, reader) = await OpenAndAwaitPreambleAsync();
        using var _ = response;

        (await reader.NextAsync()).Comment.Should().Be("keep-alive");
        (await reader.NextAsync()).Comment.Should().Be("keep-alive");
    }

    [Fact]
    public async Task AfterTheMaxLifetime_TheServerEndsTheStream_SoTheClientReconnectsWithAFreshToken()
    {
        _config["SpreadStream:MaxLifetimeMinutes"] = "0.002"; // ~120 ms
        await StartAsync();

        var (response, reader) = await OpenAndAwaitPreambleAsync();
        using var _ = response;

        (await reader.NextAsync()).IsEndOfStream.Should().BeTrue();
    }

    [Fact]
    public async Task EveryClientOfTheSameSpreadReceivesEveryEventInOrder()
    {
        const int clients = 20;
        const int events = 6;
        var opened = await Task.WhenAll(Enumerable.Range(0, clients).Select(i => OpenAndAwaitPreambleAsync(r => r.Headers.Add("X-Test-User", $"user-{i}"))));
        using var _ = new Disposables(opened.Select(o => (IDisposable)o.Response));

        for (var s = 1; s <= events; s++) await PublishTickerAsync(s);

        var received = await Task.WhenAll(opened.Select(async o =>
        {
            var ids = new List<string?>();
            for (var i = 0; i < events; i++) ids.Add((await o.Reader.NextAsync()).Id);
            return ids;
        }));
        received.Should().AllSatisfy(ids => ids.Should().Equal(Enumerable.Range(1, events).Select(s => IdOf(s).ToString())));
    }

    [Fact]
    public async Task AClientThatNeverReads_DoesNotBlockThePublisherOrOtherClients()
    {
        var (stalledResponse, _) = await OpenAndAwaitPreambleAsync(); // never read again
        var (liveResponse, live) = await OpenAndAwaitPreambleAsync();
        using var _a = stalledResponse;
        using var _b = liveResponse;

        var publishing = Task.Run(async () =>
        {
            for (var s = 1; s <= 2_000; s++) await PublishTickerAsync(s);
        });

        await publishing.WaitAsync(Timeout);
        // The healthy client keeps receiving (it may have lost the oldest events if it lagged, never the stream).
        (await live.NextAsync()).Event.Should().Be("ticker");
    }

    [Fact]
    public async Task DisconnectingTheClient_LetsTheServerSideStreamEnd()
    {
        var (response, _) = await OpenAndAwaitPreambleAsync();

        response.Dispose();

        // The server has no more subscriber to feed; publishing after a disconnect must be a harmless no-op.
        var act = () => PublishTickerAsync(1);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReconnectingWithTheLastSeenId_ResumesWithoutDuplicatesOrGaps()
    {
        var (first, firstReader) = await OpenAndAwaitPreambleAsync();
        await PublishTickerAsync(1);
        await PublishTickerAsync(2);
        (await firstReader.NextAsync()).Id.Should().Be(IdOf(1).ToString());
        var lastSeen = (await firstReader.NextAsync()).Id;
        first.Dispose();

        // Tickers 3 and 4 happen while the client is away; they are what the database would hold.
        _tickers
            .Setup(r => r.GetSinceWithoutOrderBookAsync(_spreadId, T0.AddSeconds(2), It.IsAny<int>()))
            .ReturnsAsync([BuildTicker(3, false), BuildTicker(4, false)]);

        var (second, secondReader) = await OpenAndAwaitPreambleAsync(r => r.Headers.Add("Last-Event-ID", lastSeen));
        using var _ = second;
        await PublishTickerAsync(5);

        var ids = new List<string?>();
        for (var i = 0; i < 3; i++) ids.Add((await secondReader.NextAsync()).Id);
        ids.Should().Equal(IdOf(3).ToString(), IdOf(4).ToString(), IdOf(5).ToString());
    }

    private sealed class Disposables(IEnumerable<IDisposable> items) : IDisposable
    {
        public void Dispose() { foreach (var item in items) item.Dispose(); }
    }

    private sealed class HeaderAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var user)) return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, user.ToString())], "Test");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }
}

/// <summary>Minimal text/event-stream parser: reads one block (up to the blank line) at a time.</summary>
internal sealed class SseReader(Stream stream)
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);
    private readonly StreamReader _reader = new(stream);

    public async Task<SseBlock> NextAsync()
    {
        using var cts = new CancellationTokenSource(ReadTimeout);
        var block = new SseBlock();
        var any = false;

        while (true)
        {
            var line = await _reader.ReadLineAsync(cts.Token);
            if (line is null) return any ? block : new SseBlock { IsEndOfStream = true };
            if (line.Length == 0)
            {
                if (any) return block;
                continue;
            }

            any = true;
            if (line.StartsWith(':')) block.Comment = line[1..].Trim();
            else if (line.StartsWith("id:", StringComparison.Ordinal)) block.Id = line[3..].Trim();
            else if (line.StartsWith("event:", StringComparison.Ordinal)) block.Event = line[6..].Trim();
            else if (line.StartsWith("data:", StringComparison.Ordinal)) block.Data = line[5..].Trim();
            else if (line.StartsWith("retry:", StringComparison.Ordinal)) block.Retry = int.Parse(line[6..].Trim());
        }
    }
}

internal sealed class SseBlock
{
    public string? Comment { get; set; }
    public string? Id { get; set; }
    public string? Event { get; set; }
    public string? Data { get; set; }
    public int? Retry { get; set; }
    public bool IsEndOfStream { get; set; }
}
