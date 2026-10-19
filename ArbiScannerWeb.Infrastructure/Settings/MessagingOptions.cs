namespace ArbiScannerWeb.Infrastructure.Settings;

/// <summary>Which broker a consuming service reads spread events from during the RabbitMQ -> Kafka migration.</summary>
public enum MessagingConsumerMode
{
    /// <summary>Consume from RabbitMQ only (today's behaviour, the default).</summary>
    RabbitMq,

    /// <summary>RabbitMQ stays live; a separate Kafka consumer group processes the same events with no side effects
    /// and both backends are recorded for the parity check.</summary>
    Shadow,

    /// <summary>Consume from Kafka only. Not available until the cutover phase.</summary>
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
}
