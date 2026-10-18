using System.Text;
using ArbiScannerWeb.Messaging.Kafka;
using Confluent.Kafka;
using FluentAssertions;
using Moq;

namespace ArbiScannerWeb.Tests.Messaging;

public class DeadLetterPublisherTests
{
    private static ConsumedMessage Message(Headers headers) => new(
        "spreads", 3, 42, "futures:binance:bybit:BTC-USDT.USDT", [1, 2, 3], headers,
        MessageHeaders.Read(headers), DateTime.UtcNow);

    [Fact]
    public async Task Publishes_to_the_dlq_topic_with_the_same_key_payload_and_failure_context()
    {
        var producer = new Mock<IKafkaMessageProducer>();
        string? topic = null;
        string? key = null;
        byte[]? value = null;
        Headers? sent = null;
        producer
            .Setup(p => p.ForwardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Headers>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, byte[], Headers, CancellationToken>((t, k, v, h, _) => (topic, key, value, sent) = (t, k, v, h))
            .ReturnsAsync(new ProducedMessage("spreads.dlq", 0, 0));
        var clock = new ManualTimeProvider();
        var publisher = new DeadLetterPublisher(producer.Object, clock);
        var original = new Headers { { MessageHeaders.MessageId, "m-1"u8.ToArray() }, { MessageHeaders.TraceParent, "00-aa-bb-01"u8.ToArray() } };

        await publisher.PublishAsync(Message(original), new InvalidOperationException("db down"), attempts: 3, CancellationToken.None);

        topic.Should().Be("spreads.dlq");
        key.Should().Be("futures:binance:bybit:BTC-USDT.USDT");
        value.Should().Equal(1, 2, 3);
        MessageHeaders.GetString(sent, MessageHeaders.MessageId).Should().Be("m-1");
        MessageHeaders.GetString(sent, MessageHeaders.TraceParent).Should().Be("00-aa-bb-01");
        MessageHeaders.GetString(sent, MessageHeaders.DlqError).Should().Be("InvalidOperationException: db down");
        MessageHeaders.GetString(sent, MessageHeaders.DlqAttempts).Should().Be("3");
        MessageHeaders.GetString(sent, MessageHeaders.DlqSourceTopic).Should().Be("spreads");
        MessageHeaders.GetString(sent, MessageHeaders.DlqSourcePartition).Should().Be("3");
        MessageHeaders.GetString(sent, MessageHeaders.DlqSourceOffset).Should().Be("42");
        MessageHeaders.GetString(sent, MessageHeaders.DlqFailedAt).Should().Be(clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Replaces_stale_dlq_headers_when_a_dead_lettered_message_is_replayed_and_fails_again()
    {
        var producer = new Mock<IKafkaMessageProducer>();
        Headers? sent = null;
        producer
            .Setup(p => p.ForwardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Headers>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, byte[], Headers, CancellationToken>((_, _, _, h, _) => sent = h)
            .ReturnsAsync(new ProducedMessage("spreads.dlq", 0, 0));
        var original = new Headers { { MessageHeaders.DlqError, "old failure"u8.ToArray() }, { MessageHeaders.DlqAttempts, "9"u8.ToArray() } };

        await new DeadLetterPublisher(producer.Object).PublishAsync(Message(original), new TimeoutException("slow"), 2, CancellationToken.None);

        sent!.Count(h => h.Key == MessageHeaders.DlqError).Should().Be(1);
        MessageHeaders.GetString(sent, MessageHeaders.DlqAttempts).Should().Be("2");
    }

    [Fact]
    public async Task Truncates_very_long_error_text()
    {
        var producer = new Mock<IKafkaMessageProducer>();
        Headers? sent = null;
        producer
            .Setup(p => p.ForwardAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Headers>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, byte[], Headers, CancellationToken>((_, _, _, h, _) => sent = h)
            .ReturnsAsync(new ProducedMessage("spreads.dlq", 0, 0));

        await new DeadLetterPublisher(producer.Object).PublishAsync(Message(new Headers()), new InvalidOperationException(new string('x', 5_000)), 1, CancellationToken.None);

        Encoding.UTF8.GetByteCount(MessageHeaders.GetString(sent, MessageHeaders.DlqError)!).Should().BeLessThanOrEqualTo(1_000);
    }
}
