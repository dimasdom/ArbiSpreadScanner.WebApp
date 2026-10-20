using System.Text;
using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.API.Controllers;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentAssertions;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArbiScannerWeb.Tests.API;

public class SpreadStreamControllerTests
{
    private readonly Mock<ISpreadStreamService> _streams = new();
    private readonly DefaultHttpContext _http = new();
    private readonly MemoryStream _body = new();
    private readonly Guid _id = Guid.NewGuid();

    public SpreadStreamControllerTests()
    {
        _http.Response.Body = _body;
    }

    private SpreadStreamController CreateSut(ILogger<SpreadStreamController>? logger = null) =>
        new(_streams.Object, logger ?? NullLogger<SpreadStreamController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = _http },
        };

    private string Written => Encoding.UTF8.GetString(_body.ToArray());

    private void Returns(params SpreadStreamEvent[] events) =>
        _streams.Setup(s => s.OpenAsync(_id, It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(ToAsync(events)));

    private static async IAsyncEnumerable<SpreadStreamEvent> ToAsync(IEnumerable<SpreadStreamEvent> events)
    {
        foreach (var e in events) yield return e;
        await Task.CompletedTask;
    }

    [Theory]
    [InlineData(403, StatusCodes.Status403Forbidden)]
    [InlineData(404, StatusCodes.Status404NotFound)]
    public async Task Stream_WhenTheServiceFails_ReturnsThatStatusAndWritesNoStream(int errorStatus, int expected)
    {
        var error = new Error("nope").WithMetadata(TypedErrors.HttpStatusCodeMetadataKey, errorStatus);
        _streams.Setup(s => s.OpenAsync(_id, It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<IAsyncEnumerable<SpreadStreamEvent>>(error));

        var result = await CreateSut().Stream(_id, CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(expected);
        _body.Length.Should().Be(0);
        _http.Response.ContentType.Should().BeNull();
    }

    [Fact]
    public async Task Stream_WhenTheFailureHasNoStatusMetadata_Returns500()
    {
        _streams.Setup(s => s.OpenAsync(_id, It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<IAsyncEnumerable<SpreadStreamEvent>>("plain failure"));

        var result = await CreateSut().Stream(_id, CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task Stream_SetsTheEventStreamHeaders()
    {
        Returns();

        await CreateSut().Stream(_id, CancellationToken.None);

        _http.Response.ContentType.Should().Be("text/event-stream");
        _http.Response.Headers.CacheControl.ToString().Should().Contain("no-cache").And.Contain("no-transform");
        _http.Response.Headers["X-Accel-Buffering"].ToString().Should().Be("no");
    }

    [Fact]
    public async Task Stream_WritesThePreambleThenEachEventInOrder()
    {
        Returns(
            SpreadStreamEvent.KeepAlive,
            SpreadStreamEvent.Ticker(new TradeOpportunityTickerModel { DateTime = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc) }),
            SpreadStreamEvent.Close(_id));

        var result = await CreateSut().Stream(_id, CancellationToken.None);

        result.Should().BeOfType<EmptyResult>();
        var written = Written;
        written.Should().StartWith(": connected\nretry: 3000\n\n");
        written.IndexOf(": keep-alive", StringComparison.Ordinal).Should().BeLessThan(written.IndexOf("event: ticker", StringComparison.Ordinal));
        written.IndexOf("event: ticker", StringComparison.Ordinal).Should().BeLessThan(written.IndexOf("event: close", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("1760875200000", 1760875200000L)]
    [InlineData("0", 0L)]
    [InlineData("", null)]
    [InlineData("abc", null)]
    [InlineData("-5", null)]
    [InlineData("1.5", null)]
    public async Task Stream_ParsesLastEventId_IgnoringGarbage(string header, long? expected)
    {
        Returns();
        if (header.Length > 0) _http.Request.Headers["Last-Event-ID"] = header;

        await CreateSut().Stream(_id, CancellationToken.None);

        _streams.Verify(s => s.OpenAsync(_id, expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Stream_PassesTheRequestCancellationTokenToTheService()
    {
        using var cts = new CancellationTokenSource();
        _streams.Setup(s => s.OpenAsync(_id, It.IsAny<long?>(), cts.Token)).ReturnsAsync(Result.Ok(ToAsync([])));

        await CreateSut().Stream(_id, cts.Token);

        _streams.Verify(s => s.OpenAsync(_id, It.IsAny<long?>(), cts.Token), Times.Once);
    }

    [Fact]
    public async Task Stream_WhenTheClientDisconnectsMidStream_EndsQuietly()
    {
        using var cts = new CancellationTokenSource();
        _streams.Setup(s => s.OpenAsync(_id, It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(Cancelling(cts)));

        var act = async () => await CreateSut().Stream(_id, cts.Token);

        (await act.Should().NotThrowAsync()).Subject.Should().BeOfType<EmptyResult>();

        static async IAsyncEnumerable<SpreadStreamEvent> Cancelling(CancellationTokenSource source)
        {
            yield return SpreadStreamEvent.KeepAlive;
            await source.CancelAsync();
            yield return SpreadStreamEvent.KeepAlive;
        }
    }

    [Fact]
    public async Task Stream_WhenTheSourceThrowsMidStream_LogsAndEndsInsteadOfBreakingTheStartedResponse()
    {
        var logger = new Mock<ILogger<SpreadStreamController>>();
        _streams.Setup(s => s.OpenAsync(_id, It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(Throwing()));

        var result = await CreateSut(logger.Object).Stream(_id, CancellationToken.None);

        result.Should().BeOfType<EmptyResult>();
        Written.Should().Contain(": keep-alive");
        logger.Verify(l => l.Log(
            LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);

        static async IAsyncEnumerable<SpreadStreamEvent> Throwing()
        {
            yield return SpreadStreamEvent.KeepAlive;
            await Task.Yield();
            throw new InvalidOperationException("mongo is down");
        }
    }
}
