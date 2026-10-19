using System.Diagnostics.Metrics;

namespace ArbiScannerWeb.Infrastructure.Messaging;

/// <summary>Live-consumer counters. Register with <c>.AddMeter(ConsumerMetrics.MeterName)</c>.</summary>
public static class ConsumerMetrics
{
    public const string MeterName = "ArbiScanner.Messaging.Consumer";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> DuplicatesSkipped = Meter.CreateCounter<long>(
        "arbiscanner.messaging.consumer.duplicates_skipped",
        description: "Messages skipped because their message-id was already processed.");

    public static readonly Counter<long> StaleAlertsSkipped = Meter.CreateCounter<long>(
        "arbiscanner.messaging.consumer.stale_alerts_skipped",
        description: "Open events not alerted on because they were older than the replay-protection window.");
}
