using System.Threading.Channels;
using ArbiScannerWeb.Domain.Models.DTOs;

namespace ArbiScannerWeb.Abstractions.Interfaces;

/// <summary>In-process fan-out of spread events to the open SSE streams of that spread. Single-replica by design: there is no
/// cross-instance backplane, so a second API instance would only reach its own subscribers.</summary>
public interface ISpreadStreamBroker
{
    /// <summary>Starts receiving events published for <paramref name="spreadId"/>. Dispose the subscription to stop.</summary>
    ISpreadStreamSubscription Subscribe(Guid spreadId);

    /// <summary>Delivers <paramref name="streamEvent"/> to every current subscriber of the spread. Never blocks: a subscriber
    /// whose buffer is full loses its oldest event instead of slowing the Kafka consumer down.</summary>
    void Publish(Guid spreadId, SpreadStreamEvent streamEvent);
}

public interface ISpreadStreamSubscription : IDisposable
{
    ChannelReader<SpreadStreamEvent> Reader { get; }
}
