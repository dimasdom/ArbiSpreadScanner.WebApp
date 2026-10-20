namespace ArbiScannerWeb.Domain.Models.DTOs;

/// <summary>One server-sent event on a spread's live stream. <see cref="Id"/> is the SSE <c>id:</c> field (the ticker's
/// Unix-millisecond timestamp), which the client sends back as <c>Last-Event-ID</c> to resume without a gap.</summary>
public sealed record SpreadStreamEvent(string Type, string? Id = null, object? Data = null)
{
    public static SpreadStreamEvent KeepAlive { get; } = new(SpreadStreamEventTypes.KeepAlive);

    public static SpreadStreamEvent Ticker(TradeOpportunityTickerModel ticker) =>
        new(SpreadStreamEventTypes.Ticker, new DateTimeOffset(DateTime.SpecifyKind(ticker.DateTime, DateTimeKind.Utc)).ToUnixTimeMilliseconds().ToString(), ticker);

    public static SpreadStreamEvent Close(Guid spreadId) =>
        new(SpreadStreamEventTypes.Close, Data: new { spreadId });
}

public static class SpreadStreamEventTypes
{
    public const string Ticker = "ticker";
    public const string Close = "close";

    /// <summary>Not a data event: written as an SSE comment so idle proxies don't cut the connection.</summary>
    public const string KeepAlive = "keepalive";
}
