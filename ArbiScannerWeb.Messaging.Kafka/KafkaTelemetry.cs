using System.Diagnostics;
using Confluent.Kafka;

namespace ArbiScannerWeb.Messaging.Kafka;

/// <summary>Trace propagation across Kafka. Span names and attributes follow the OpenTelemetry messaging
/// semantic conventions. Register the source with <c>.AddSource(KafkaTelemetry.SourceName)</c>.</summary>
public static class KafkaTelemetry
{
    public const string SourceName = "ArbiScanner.Messaging.Kafka";

    internal static readonly ActivitySource Source = new(SourceName);

    internal static Activity? StartProducer(string topic, string key)
    {
        var activity = Source.StartActivity($"{topic} publish", ActivityKind.Producer);
        if (activity is not null)
        {
            activity.SetTag("messaging.system", "kafka");
            activity.SetTag("messaging.operation.type", "publish");
            activity.SetTag("messaging.destination.name", topic);
            activity.SetTag("messaging.kafka.message.key", key);
        }

        return activity;
    }

    internal static Activity? StartConsumer(ConsumedMessage message, string groupId)
    {
        ActivityContext.TryParse(message.Metadata.TraceParent, message.Metadata.TraceState, isRemote: true, out var parent);

        var activity = Source.StartActivity($"{message.Topic} process", ActivityKind.Consumer, parent);
        if (activity is not null)
        {
            activity.SetTag("messaging.system", "kafka");
            activity.SetTag("messaging.operation.type", "process");
            activity.SetTag("messaging.destination.name", message.Topic);
            activity.SetTag("messaging.consumer.group.name", groupId);
            activity.SetTag("messaging.destination.partition.id", message.Partition.ToString(System.Globalization.CultureInfo.InvariantCulture));
            activity.SetTag("messaging.kafka.message.key", message.Key);
            activity.SetTag("messaging.kafka.offset", message.Offset);
            if (message.Metadata.MessageId is { } id)
            {
                activity.SetTag("messaging.message.id", id);
            }
        }

        return activity;
    }

    internal static void RecordFailure(Activity? activity, Exception exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.SetTag("error.type", exception.GetType().FullName);
    }
}
