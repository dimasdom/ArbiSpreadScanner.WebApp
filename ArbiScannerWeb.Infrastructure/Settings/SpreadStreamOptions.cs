namespace ArbiScannerWeb.Infrastructure.Settings;

/// <summary>Bound from the <c>SpreadStream</c> section (env vars: <c>SpreadStream__KeepAliveSeconds</c>, ...).</summary>
public sealed class SpreadStreamOptions
{
    public const string SectionName = "SpreadStream";

    /// <summary>Events buffered per open stream; when full the oldest is dropped.</summary>
    public int SubscriberBufferSize { get; set; } = 256;

    /// <summary>Idle interval after which a keep-alive comment is written.</summary>
    public int KeepAliveSeconds { get; set; } = 15;

    /// <summary>The server ends a stream after this long so the client reconnects with a fresh access token (the token is only
    /// validated when the connection opens).</summary>
    public int MaxLifetimeMinutes { get; set; } = 10;

    /// <summary>Most missed tickers replayed to a reconnecting client.</summary>
    public int ReplayLimit { get; set; } = 500;
}
