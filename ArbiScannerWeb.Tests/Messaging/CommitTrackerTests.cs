using ArbiScannerWeb.Messaging.Kafka;
using Confluent.Kafka;
using FluentAssertions;

namespace ArbiScannerWeb.Tests.Messaging;

public class CommitTrackerTests
{
    private static TopicPartitionOffset Tpo(int partition, long offset) => new("spreads", partition, offset);

    [Fact]
    public void Does_not_commit_when_nothing_was_processed_even_after_the_interval()
    {
        var clock = new ManualTimeProvider();
        var tracker = new CommitTracker(10, TimeSpan.FromSeconds(5), clock);

        clock.Advance(TimeSpan.FromMinutes(1));

        tracker.ShouldCommit().Should().BeFalse();
    }

    [Fact]
    public void Commits_when_the_batch_size_is_reached()
    {
        var tracker = new CommitTracker(3, TimeSpan.FromHours(1), new ManualTimeProvider());

        tracker.Record(Tpo(0, 0));
        tracker.Record(Tpo(0, 1));
        tracker.ShouldCommit().Should().BeFalse();

        tracker.Record(Tpo(0, 2));
        tracker.ShouldCommit().Should().BeTrue();
    }

    [Fact]
    public void Commits_when_the_interval_elapsed_with_pending_work()
    {
        var clock = new ManualTimeProvider();
        var tracker = new CommitTracker(100, TimeSpan.FromSeconds(5), clock);

        tracker.Record(Tpo(0, 0));
        tracker.ShouldCommit().Should().BeFalse();

        clock.Advance(TimeSpan.FromSeconds(5));
        tracker.ShouldCommit().Should().BeTrue();
    }

    [Fact]
    public void Drain_returns_the_next_offset_to_read_per_partition_and_resets()
    {
        var tracker = new CommitTracker(100, TimeSpan.FromHours(1), new ManualTimeProvider());
        tracker.Record(Tpo(0, 4));
        tracker.Record(Tpo(1, 9));
        tracker.Record(Tpo(0, 5));

        var offsets = tracker.Drain();

        offsets.Should().BeEquivalentTo(new[] { Tpo(0, 6), Tpo(1, 10) });
        tracker.PendingCount.Should().Be(0);
        tracker.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Record_never_moves_an_offset_backwards()
    {
        var tracker = new CommitTracker(100, TimeSpan.FromHours(1), new ManualTimeProvider());
        tracker.Record(Tpo(0, 7));
        tracker.Record(Tpo(0, 3));

        tracker.Drain().Should().ContainSingle().Which.Offset.Value.Should().Be(8);
    }

    [Fact]
    public void Drain_resets_the_interval_window()
    {
        var clock = new ManualTimeProvider();
        var tracker = new CommitTracker(100, TimeSpan.FromSeconds(5), clock);
        tracker.Record(Tpo(0, 0));
        clock.Advance(TimeSpan.FromSeconds(6));
        tracker.Drain();

        tracker.Record(Tpo(0, 1));

        tracker.ShouldCommit().Should().BeFalse();
    }

    [Fact]
    public void Forget_drops_pending_offsets_for_partitions_that_were_taken_away()
    {
        var tracker = new CommitTracker(100, TimeSpan.FromHours(1), new ManualTimeProvider());
        tracker.Record(Tpo(0, 1));
        tracker.Record(Tpo(1, 1));

        tracker.Forget([new TopicPartition("spreads", 0)]);

        tracker.Drain().Should().ContainSingle().Which.Partition.Value.Should().Be(1);
    }
}
