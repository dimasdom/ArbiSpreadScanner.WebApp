namespace ArbiScannerWeb.Infrastructure.Settings;

/// <summary>Bound from the <c>Messaging</c> section (env vars: <c>Messaging__ServiceName</c>,
/// <c>Messaging__Consumer__MaxOpenAlertAgeMinutes</c>).</summary>
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>Stable name of the consuming service (e.g. <c>webapp</c>, <c>telegram</c>). Derives the Kafka consumer group
    /// (<c>{ServiceName}-spreads</c>) and scopes duplicate-suppression keys.</summary>
    public string ServiceName { get; set; } = string.Empty;

    public MessagingConsumerSettings Consumer { get; set; } = new();
}

public sealed class MessagingConsumerSettings
{
    /// <summary>Replay protection for user-facing alerts (Telegram): an Open event whose producer timestamp is older than this
    /// is not alerted on. Without it, a long outage or an offset reset would re-send alerts for spreads that are long gone.</summary>
    public int MaxOpenAlertAgeMinutes { get; set; } = 10;
}
