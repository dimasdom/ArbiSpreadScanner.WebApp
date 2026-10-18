using System.Collections.Concurrent;
using System.Text;
using ArbiScannerWeb.Messaging.Kafka;
using FluentAssertions;
using static ArbiScannerWeb.IntegrationTests.Kafka.KafkaTestSupport;

namespace ArbiScannerWeb.IntegrationTests.Kafka;

[Collection(KafkaCollection.Name)]
public sealed class KafkaMessagingTests(KafkaTestFixture kafka)
{
    private static string GroupId() => "group-" + Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task Messages_with_the_same_key_arrive_in_order()
    {
        var topic = await kafka.CreateTopicAsync(partitions: 3);
        using var producer = CreateProducer(kafka.BootstrapServers);
        string[] keys = ["k0", "k1", "k2", "k3", "k4", "k5"];
        const int perKey = 40;

        for (var seq = 0; seq < perKey; seq++)
        {
            foreach (var key in keys)
            {
                await SendAsync(producer, topic, key, seq.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        await using var host = await ConsumerHost.StartAsync(kafka.BootstrapServers, topic, GroupId());
        await UntilAsync(() => host.Probe.Received.Count >= keys.Length * perKey, "all messages consumed");

        foreach (var key in keys)
        {
            var sequence = host.Probe.Received
                .Where(m => m.Key == key)
                .Select(m => int.Parse(host.Probe.Payload(m), System.Globalization.CultureInfo.InvariantCulture))
                .ToList();

            sequence.Should().Equal(Enumerable.Range(0, perKey), $"key {key} must be consumed in produce order");
        }
    }

    [Fact]
    public async Task A_restarted_consumer_resumes_from_the_last_committed_offset_without_loss()
    {
        var topic = await kafka.CreateTopicAsync(partitions: 2);
        var group = GroupId();
        using var producer = CreateProducer(kafka.BootstrapServers);

        for (var i = 0; i < 10; i++)
        {
            await SendAsync(producer, topic, $"k{i % 3}", $"first-{i}");
        }

        string[] firstRun;
        await using (var first = await ConsumerHost.StartAsync(kafka.BootstrapServers, topic, group))
        {
            await UntilAsync(() => first.Probe.Received.Count >= 10, "first run consumed 10");
            await first.StopAsync();
            firstRun = first.Probe.Received.Select(m => first.Probe.Payload(m)).ToArray();
        }

        for (var i = 0; i < 10; i++)
        {
            await SendAsync(producer, topic, $"k{i % 3}", $"second-{i}");
        }

        await using var second = await ConsumerHost.StartAsync(kafka.BootstrapServers, topic, group);
        await UntilAsync(() => second.Probe.Received.Count >= 10, "second run consumed the 10 new messages");
        await Task.Delay(1_000);

        var secondRun = second.Probe.Received.Select(m => second.Probe.Payload(m)).ToArray();
        secondRun.Should().BeEquivalentTo(Enumerable.Range(0, 10).Select(i => $"second-{i}"), "nothing may be lost and committed messages must not be replayed");
        firstRun.Should().HaveCount(10);
    }

    [Fact]
    public async Task A_duplicate_message_produces_no_duplicate_side_effect()
    {
        var topic = await kafka.CreateTopicAsync(partitions: 1);
        using var producer = CreateProducer(kafka.BootstrapServers);
        var store = new InMemoryIdempotencyStore();
        var sideEffects = new ConcurrentQueue<string>();

        await SendAsync(producer, topic, "k", "open", messageId: "msg-1");
        await SendAsync(producer, topic, "k", "open", messageId: "msg-1");
        await SendAsync(producer, topic, "k", "update", messageId: "msg-2");

        await using var host = await ConsumerHost.StartAsync(kafka.BootstrapServers, topic, GroupId(), probe =>
            probe.Behaviour = (message, ct) => IdempotentExecutor.RunOnceAsync(
                store,
                $"dedupe:{message.Metadata.MessageId}",
                _ =>
                {
                    sideEffects.Enqueue(probe.Payload(message));
                    return Task.CompletedTask;
                },
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(10),
                ct));

        await UntilAsync(() => host.Probe.Received.Count >= 3, "all three deliveries handled");

        sideEffects.Should().Equal("open", "update");
    }

    [Fact]
    public async Task A_poison_message_lands_in_the_dlq_and_later_messages_still_flow()
    {
        var topic = await kafka.CreateTopicAsync(partitions: 1);
        using var producer = CreateProducer(kafka.BootstrapServers);

        await SendAsync(producer, topic, "k", "ok-1", messageId: "m-1");
        await SendAsync(producer, topic, "k", "poison", messageId: "m-poison");
        await SendAsync(producer, topic, "k", "ok-2", messageId: "m-2");

        await using var host = await ConsumerHost.StartAsync(kafka.BootstrapServers, topic, GroupId(), probe =>
            probe.Behaviour = (message, _) => probe.Payload(message) == "poison"
                ? throw new InvalidOperationException("cannot process poison")
                : Task.CompletedTask);

        await UntilAsync(() => host.Probe.Received.Any(m => host.Probe.Payload(m) == "ok-2"), "message after the poison one was processed");

        var dead = ReadAll(kafka.BootstrapServers, KafkaTopics.DeadLetterOf(topic), expected: 1);
        var letter = dead.Should().ContainSingle().Subject;
        letter.Message.Key.Should().Be("k");
        Encoding.UTF8.GetString(letter.Message.Value).Should().Be("poison");
        MessageHeaders.GetString(letter.Message.Headers, MessageHeaders.DlqAttempts).Should().Be("2");
        MessageHeaders.GetString(letter.Message.Headers, MessageHeaders.DlqError).Should().Contain("cannot process poison");
        MessageHeaders.GetString(letter.Message.Headers, MessageHeaders.DlqSourceTopic).Should().Be(topic);
        MessageHeaders.GetString(letter.Message.Headers, MessageHeaders.MessageId).Should().Be("m-poison");
        host.Probe.Attempts["m-poison"].Should().Be(2, "a failing message is retried a bounded number of times");
    }

    [Fact]
    public async Task A_non_retryable_message_is_dead_lettered_after_a_single_attempt()
    {
        var topic = await kafka.CreateTopicAsync(partitions: 1);
        using var producer = CreateProducer(kafka.BootstrapServers);
        await SendAsync(producer, topic, "k", "garbage", messageId: "m-bad");

        await using var host = await ConsumerHost.StartAsync(kafka.BootstrapServers, topic, GroupId(), probe =>
            probe.Behaviour = (_, _) => throw new NonRetryableMessageException("undecodable"), maxHandlerAttempts: 5);

        var dead = ReadAll(kafka.BootstrapServers, KafkaTopics.DeadLetterOf(topic), expected: 1);

        dead.Should().ContainSingle();
        host.Probe.Attempts["m-bad"].Should().Be(1);
    }

    [Fact]
    public async Task Two_consumer_instances_share_the_work_and_nothing_is_lost_or_applied_twice()
    {
        var topic = await kafka.CreateTopicAsync(partitions: 4);
        var group = GroupId();
        using var producer = CreateProducer(kafka.BootstrapServers);
        var store = new InMemoryIdempotencyStore();
        var effects = new ConcurrentDictionary<string, int>();

        void Configure(HandlerProbe probe) =>
            probe.Behaviour = (message, ct) => IdempotentExecutor.RunOnceAsync(
                store,
                $"dedupe:{message.Metadata.MessageId}",
                _ =>
                {
                    effects.AddOrUpdate(message.Metadata.MessageId!, 1, (_, n) => n + 1);
                    return Task.CompletedTask;
                },
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(10),
                ct);

        var produced = 0;
        async Task ProduceAsync(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var n = Interlocked.Increment(ref produced);
                await SendAsync(producer, topic, $"key-{n % 8}", n.ToString(System.Globalization.CultureInfo.InvariantCulture), messageId: $"id-{n}");
            }
        }

        await ProduceAsync(20);
        await using var a = await ConsumerHost.StartAsync(kafka.BootstrapServers, topic, group, Configure);
        await UntilAsync(() => effects.Count >= 20, "instance A consumed the initial batch");

        await using var b = await ConsumerHost.StartAsync(kafka.BootstrapServers, topic, group, Configure);
        await UntilAsync(
            () =>
            {
                ProduceAsync(1).GetAwaiter().GetResult();
                return !b.Probe.Received.IsEmpty;
            },
            "instance B received work after the rebalance");

        var total = produced;
        await UntilAsync(() => effects.Count >= total, "every produced message has been applied");

        effects.Should().HaveCount(total);
        effects.Values.Should().OnlyContain(n => n == 1, "a redelivery after the rebalance must not repeat a side effect");
        a.Probe.Received.Should().NotBeEmpty();
    }
}
