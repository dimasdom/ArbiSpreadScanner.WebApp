namespace ArbiScannerWeb.Domain.Models.DTOs;

/// <summary>One server-sent event on a spread's live stream. <see cref="Id"/> is the SSE <c>id:</c> field (the ticker's
/// Unix-millisecond timestamp), which the client sends back as <c>Last-Event-ID</c> to resume without a gap.</summary>
public sealed record SpreadStreamEvent(string Type, string? Id = null, object? Data = null)
{
    public static SpreadStreamEvent KeepAlive { get; } = new(SpreadStreamEventTypes.KeepAlive);

    public static SpreadStreamEvent Ticker(TradeOpportunityTickerModel ticker) =>
        new(SpreadStreamEventTypes.Ticker, ToEventId(ticker.DateTime), ticker);

    /// <summary>Unix milliseconds, the precision MongoDB stores, so an id taken from a live event and one read back from the
    /// database for the same ticker are equal. <c>Unspecified</c> is treated as UTC (that is what the app writes).</summary>
    public static string ToEventId(DateTime timestamp)
    {
        var utc = timestamp.Kind == DateTimeKind.Local ? timestamp.ToUniversalTime() : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
        return new DateTimeOffset(utc).ToUnixTimeMilliseconds().ToString();
    }

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
