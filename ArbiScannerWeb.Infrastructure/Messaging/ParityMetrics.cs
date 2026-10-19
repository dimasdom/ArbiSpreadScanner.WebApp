using System.Diagnostics.Metrics;

namespace ArbiScannerWeb.Infrastructure.Messaging;

/// <summary>Counters for the RabbitMQ/Kafka parity check. Register with <c>.AddMeter(ParityMetrics.MeterName)</c>.</summary>
public static class ParityMetrics
{
    public const string MeterName = "ArbiScanner.Messaging.Parity";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> ComparedMinutes = Meter.CreateCounter<long>(
        "arbiscanner.messaging.parity.compared_minutes",
        description: "Closed minute buckets that were compared between RabbitMQ and Kafka.");

    public static readonly Counter<long> Matched = Meter.CreateCounter<long>(
        "arbiscanner.messaging.parity.matched",
        description: "Messages delivered by both backends.");

    public static readonly Counter<long> MissingInKafka = Meter.CreateCounter<long>(
        "arbiscanner.messaging.parity.missing_in_kafka",
        description: "Messages RabbitMQ delivered that Kafka did not.");

    public static readonly Counter<long> MissingInRabbitMq = Meter.CreateCounter<long>(
        "arbiscanner.messaging.parity.missing_in_rabbitmq",
        description: "Messages Kafka delivered that RabbitMQ did not.");

    public static readonly Counter<long> KeyMismatches = Meter.CreateCounter<long>(
        "arbiscanner.messaging.parity.key_mismatch",
        description: "Kafka messages whose partition key differs from the key derived from the payload.");
}
