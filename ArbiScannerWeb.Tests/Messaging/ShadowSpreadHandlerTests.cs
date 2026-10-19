using System.Diagnostics.Metrics;
using ArbiScannerWeb.Abstractions.Messaging;
using ArbiScannerWeb.Infrastructure.Messaging;
using ArbiScannerWeb.Infrastructure.Settings;
using ArbiScannerWeb.Messaging.Kafka;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ArbiScannerWeb.Tests.Messaging;

public class ShadowSpreadHandlerTests
{
    private readonly Mock<IParityRecorder> _parity = new();

    private ShadowSpreadHandler Sut() => new(
        _parity.Object,
        Options.Create(new MessagingOptions { ServiceName = "webapp" }),
        NullLogger<ShadowSpreadHandler>.Instance);

    private static ConsumedMessage Message(byte[] payload, string key, string? messageId, DateTime? at = null)
    {
        var headers = new Headers();
        if (messageId is not null)
        {
            headers.Add(MessageHeaders.MessageId, System.Text.Encoding.UTF8.GetBytes(messageId));
        }

        return new ConsumedMessage("spreads", 1, 7, key, payload, headers, MessageHeaders.Read(headers), at ?? new DateTime(2026, 5, 1, 10, 30, 15, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Records_the_message_for_the_parity_check_using_the_producer_timestamp()
    {
        var model = MessagingTestData.Spread();
        var key = SpreadEventKeys.For(model);

        await Sut().HandleAsync(Message(TradeOpportunitySerializer.Serialize(model), key, "id-1"), CancellationToken.None);

        _parity.Verify(p => p.RecordAsync(MessagingBackends.Kafka, key, "id-1", new DateTimeOffset(2026, 5, 1, 10, 30, 15, TimeSpan.Zero)), Times.Once);
    }

    [Fact]
    public async Task A_message_without_a_message_id_cannot_be_matched_and_is_not_recorded()
    {
        var model = MessagingTestData.Spread();

        await Sut().HandleAsync(Message(TradeOpportunitySerializer.Serialize(model), SpreadEventKeys.For(model), messageId: null), CancellationToken.None);

        _parity.Verify(p => p.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task An_undecodable_payload_is_non_retryable_so_it_goes_straight_to_the_dlq()
    {
        var act = () => Sut().HandleAsync(Message([0xFF, 0xFF, 0xFF, 0xFF, 0xFF], "k", "id-2"), CancellationToken.None);

        await act.Should().ThrowAsync<NonRetryableMessageException>();
    }

    [Fact]
    public async Task A_key_that_differs_from_the_payload_derived_key_is_counted()
    {
        var mismatches = 0L;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == ParityMetrics.MeterName && instrument.Name.EndsWith("key_mismatch", StringComparison.Ordinal))
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref mismatches, value));
        listener.Start();
        var model = MessagingTestData.Spread();

        await Sut().HandleAsync(Message(TradeOpportunitySerializer.Serialize(model), "wrong:key", "id-3"), CancellationToken.None);

        mismatches.Should().Be(1);
    }

    [Fact]
    public async Task Has_no_side_effect_beyond_one_parity_record()
    {
        var model = MessagingTestData.Spread();

        await Sut().HandleAsync(Message(TradeOpportunitySerializer.Serialize(model), SpreadEventKeys.For(model), "id-4"), CancellationToken.None);

        _parity.Verify(p => p.RecordAsync(MessagingBackends.Kafka, It.IsAny<string>(), "id-4", It.IsAny<DateTimeOffset>()), Times.Once);
        _parity.VerifyNoOtherCalls();
    }
}
