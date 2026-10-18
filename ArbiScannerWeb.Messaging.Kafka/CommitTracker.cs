using Confluent.Kafka;

namespace ArbiScannerWeb.Messaging.Kafka;

/// <summary>
/// Tracks the highest processed offset per partition and decides when to commit: every N messages or every
/// T seconds, never per message. Offsets are only recorded after a message is fully handled (or dead-lettered),
/// so a commit never runs ahead of processing.
/// </summary>
public sealed class CommitTracker
{
    private readonly int _batchSize;
    private readonly TimeSpan _interval;
    private readonly TimeProvider _clock;
    private readonly Dictionary<TopicPartition, long> _processed = [];
    private int _count;
    private DateTimeOffset _lastCommit;

    public CommitTracker(int batchSize, TimeSpan interval, TimeProvider? clock = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        _batchSize = batchSize;
        _interval = interval;
        _clock = clock ?? TimeProvider.System;
        _lastCommit = _clock.GetUtcNow();
    }

    public int PendingCount => _count;

    public void Record(TopicPartitionOffset processed)
    {
        ArgumentNullException.ThrowIfNull(processed);

        var partition = processed.TopicPartition;
        if (!_processed.TryGetValue(partition, out var current) || processed.Offset.Value > current)
        {
            _processed[partition] = processed.Offset.Value;
        }

        _count++;
    }

    public bool ShouldCommit()
    {
        if (_count == 0)
        {
            return false;
        }

        return _count >= _batchSize || _clock.GetUtcNow() - _lastCommit >= _interval;
    }

    /// <summary>Returns the offsets to commit (last processed + 1, Kafka's "next to read" convention) and resets.</summary>
    public IReadOnlyList<TopicPartitionOffset> Drain()
    {
        var offsets = _processed
            .Select(p => new TopicPartitionOffset(p.Key, new Offset(p.Value + 1)))
            .ToList();

        _processed.Clear();
        _count = 0;
        _lastCommit = _clock.GetUtcNow();
        return offsets;
    }

    /// <summary>Drops pending state for partitions this consumer no longer owns (without committing them).</summary>
    public void Forget(IEnumerable<TopicPartition> partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);

        foreach (var partition in partitions)
        {
            _processed.Remove(partition);
        }

        _count = _processed.Count == 0 ? 0 : _count;
    }
}
