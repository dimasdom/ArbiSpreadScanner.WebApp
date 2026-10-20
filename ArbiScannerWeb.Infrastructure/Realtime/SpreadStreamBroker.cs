using System.Threading.Channels;
using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace ArbiScannerWeb.Infrastructure.Realtime;

/// <summary>
/// Register as a singleton. Subscribers are kept in copy-on-write arrays so <see cref="Publish"/> (the hot path, called from the
/// Kafka consumer) only takes the lock to read a reference, and writes to each channel outside it.
/// </summary>
public sealed class SpreadStreamBroker(IOptions<SpreadStreamOptions> options) : ISpreadStreamBroker
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Subscription[]> _subscribers = [];
    private readonly int _bufferSize = Math.Max(1, options.Value.SubscriberBufferSize);

    public ISpreadStreamSubscription Subscribe(Guid spreadId)
    {
        var channel = Channel.CreateBounded(
            new BoundedChannelOptions(_bufferSize)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            },
            (SpreadStreamEvent _) => SpreadStreamMetrics.EventsDropped.Add(1));

        var subscription = new Subscription(this, spreadId, channel);
        lock (_gate)
        {
            _subscribers[spreadId] = _subscribers.TryGetValue(spreadId, out var current)
                ? [.. current, subscription]
                : [subscription];
        }

        return subscription;
    }

    public void Publish(Guid spreadId, SpreadStreamEvent streamEvent)
    {
        ArgumentNullException.ThrowIfNull(streamEvent);

        Subscription[]? targets;
        lock (_gate)
        {
            _subscribers.TryGetValue(spreadId, out targets);
        }

        if (targets is null) return;

        foreach (var target in targets)
        {
            target.Writer.TryWrite(streamEvent);
        }
    }

    private void Unsubscribe(Subscription subscription)
    {
        lock (_gate)
        {
            if (!_subscribers.TryGetValue(subscription.SpreadId, out var current)) return;

            var remaining = current.Where(s => !ReferenceEquals(s, subscription)).ToArray();
            if (remaining.Length == 0)
            {
                _subscribers.Remove(subscription.SpreadId);
            }
            else
            {
                _subscribers[subscription.SpreadId] = remaining;
            }
        }
    }

    private sealed class Subscription(SpreadStreamBroker owner, Guid spreadId, Channel<SpreadStreamEvent> channel) : ISpreadStreamSubscription
    {
        private int _disposed;

        public Guid SpreadId { get; } = spreadId;

        public ChannelWriter<SpreadStreamEvent> Writer => channel.Writer;

        public ChannelReader<SpreadStreamEvent> Reader => channel.Reader;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

            owner.Unsubscribe(this);
            channel.Writer.TryComplete();
        }
    }
}
