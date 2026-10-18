namespace ArbiScannerWeb.Messaging.Kafka;

/// <summary>Connection and producer settings shared by every service that talks to Kafka.
/// Bound from the <c>Kafka</c> configuration section (env vars: <c>Kafka__BootstrapServers</c>, ...).</summary>
public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";

    public string ClientId { get; set; } = "arbiscanner";

    public string SpreadsTopic { get; set; } = KafkaTopics.Spreads;

    /// <summary>Upper bound for a produce call, including retries. A produce that cannot be
    /// delivered inside this window fails instead of blocking the caller indefinitely.</summary>
    public int DeliveryTimeoutMs { get; set; } = 30_000;

    public int LingerMs { get; set; } = 5;

    public int FlushTimeoutSeconds { get; set; } = 10;
}

/// <summary>Settings for one consumer group. Bound from <c>Kafka:Consumer</c>.</summary>
public sealed class KafkaConsumerOptions
{
    public const string SectionName = "Kafka:Consumer";

    public string GroupId { get; set; } = string.Empty;

    public string Topic { get; set; } = KafkaTopics.Spreads;

    /// <summary>Where a group with no committed offset starts. <c>Latest</c> by default so a new live group
    /// never replays history into user-facing side effects (e.g. Telegram alerts).</summary>
    public string AutoOffsetReset { get; set; } = "Latest";

    public int CommitBatchSize { get; set; } = 100;

    public int CommitIntervalSeconds { get; set; } = 5;

    public int PollTimeoutMs { get; set; } = 1_000;

    /// <summary>Longest a single handler call may run before the broker evicts the consumer from the group.</summary>
    public int MaxPollIntervalMs { get; set; } = 600_000;

    public int SessionTimeoutMs { get; set; } = 30_000;

    public int MaxHandlerAttempts { get; set; } = 3;

    public int RetryBaseDelayMs { get; set; } = 500;

    public int RetryMaxDelayMs { get; set; } = 10_000;
}

public static class KafkaTopics
{
    public const string Spreads = "spreads";

    public const string DeadLetterSuffix = ".dlq";

    public static string DeadLetterOf(string topic) => topic + DeadLetterSuffix;
}
