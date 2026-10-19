namespace ArbiScannerWeb.Infrastructure.Settings;

/// <summary>Which broker a consuming service reads spread events from during the RabbitMQ -> Kafka migration.</summary>
public enum MessagingConsumerMode
{
    /// <summary>Consume from RabbitMQ only (today's behaviour, the default).</summary>
    RabbitMq,

    /// <summary>RabbitMQ stays live; a separate Kafka consumer group processes the same events with no side effects
    /// and both backends are recorded for the parity check.</summary>
    Shadow,

    /// <summary>Consume from Kafka only (consumer group <c>{ServiceName}-spreads</c>). RabbitMQ is not consumed at all.</summary>
    Kafka,
}

/// <summary>Bound from the <c>Messaging</c> section (env vars: <c>Messaging__ServiceName</c>, <c>Messaging__Consumer__Mode</c>).</summary>
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>Stable name of the consuming service (e.g. <c>webapp</c>, <c>telegram</c>). Scopes duplicate-suppression keys
    /// and consumer-group names so both brokers agree on what has been processed.</summary>
    public string ServiceName { get; set; } = string.Empty;

    public MessagingConsumerSettings Consumer { get; set; } = new();
}

public sealed class MessagingConsumerSettings
{
    public MessagingConsumerMode Mode { get; set; } = MessagingConsumerMode.RabbitMq;

    /// <summary>Replay protection for user-facing alerts (Telegram): an Open event whose producer timestamp is older than this
    /// is not alerted on. Without it, a long outage or an offset reset would re-send alerts for spreads that are long gone.</summary>
    public int MaxOpenAlertAgeMinutes { get; set; } = 10;
}

public static class MessagingConfigurationExtensions
{
    /// <summary>Reads <c>Messaging:Consumer:Mode</c> (default <see cref="MessagingConsumerMode.RabbitMq"/>). Hosts use it to decide
    /// which broker consumer to register at all.</summary>
    public static MessagingConsumerMode GetConsumerMode(this Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var raw = configuration[$"{MessagingOptions.SectionName}:Consumer:Mode"];
        return Enum.TryParse<MessagingConsumerMode>(raw, ignoreCase: true, out var mode) ? mode : MessagingConsumerMode.RabbitMq;
    }
}
