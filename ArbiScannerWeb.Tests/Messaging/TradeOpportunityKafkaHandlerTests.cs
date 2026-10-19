using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Messaging;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.Messaging;
using ArbiScannerWeb.Infrastructure.Settings;
using ArbiScannerWeb.Messaging.Kafka;
using Confluent.Kafka;
using FluentAssertions;
using FluentResults;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ArbiScannerWeb.Tests.Messaging;

public class TradeOpportunityKafkaHandlerTests
{
    private sealed class MemoryStore : IIdempotencyStore
    {
        private readonly Dictionary<string, string> _state = [];

        public List<string> Keys => [.. _state.Keys];

        public Task<ClaimResult> TryBeginAsync(string key, CancellationToken cancellationToken)
        {
            if (_state.TryAdd(key, "processing"))
            {
                return Task.FromResult(ClaimResult.Started);
            }

            return Task.FromResult(_state[key] == "done" ? ClaimResult.AlreadyDone : ClaimResult.InProgress);
        }

        public Task CompleteAsync(string key)
        {
            _state[key] = "done";
            return Task.CompletedTask;
        }

        public Task AbandonAsync(string key)
        {
            _state.Remove(key);
            return Task.CompletedTask;
        }
    }

    private readonly Mock<ITradeOpportunityService> _spreads = new();
    private readonly MemoryStore _store = new();

    public TradeOpportunityKafkaHandlerTests()
    {
        _spreads.Setup(s => s.AddSpread(It.IsAny<TradeOpportunityModel>())).ReturnsAsync(Result.Ok());
        _spreads.Setup(s => s.UpdateSpread(It.IsAny<TradeOpportunityModel>())).ReturnsAsync(Result.Ok());
        _spreads.Setup(s => s.CloseSpread(It.IsAny<TradeOpportunityModel>())).ReturnsAsync(Result.Ok());
    }

    private TradeOpportunityKafkaHandler Sut() => new(
        _spreads.Object,
        _store,
        Options.Create(new MessagingOptions { ServiceName = "webapp" }),
        NullLogger<TradeOpportunityKafkaHandler>.Instance);

    private static ConsumedMessage Message(TradeOpportunityModel model, string? messageId = "m-1") => Raw(TradeOpportunitySerializer.Serialize(model), messageId);

    private static ConsumedMessage Raw(byte[] payload, string? messageId)
    {
        var headers = new Headers();
        if (messageId is not null)
        {
            headers.Add(MessageHeaders.MessageId, System.Text.Encoding.UTF8.GetBytes(messageId));
        }

        return new ConsumedMessage("spreads", 0, 1, "k", payload, headers, MessageHeaders.Read(headers), DateTime.UtcNow);
    }

    [Theory]
    [InlineData(MarketPositionAction.Open)]
    [InlineData(MarketPositionAction.Update)]
    [InlineData(MarketPositionAction.Close)]
    public async Task Dispatches_each_action_to_the_matching_service_method(MarketPositionAction action)
    {
        var model = MessagingTestData.Spread(action: action);

        await Sut().HandleAsync(Message(model), CancellationToken.None);

        _spreads.Verify(s => s.AddSpread(It.IsAny<TradeOpportunityModel>()), Times.Exactly(action == MarketPositionAction.Open ? 1 : 0));
        _spreads.Verify(s => s.UpdateSpread(It.IsAny<TradeOpportunityModel>()), Times.Exactly(action == MarketPositionAction.Update ? 1 : 0));
        _spreads.Verify(s => s.CloseSpread(It.IsAny<TradeOpportunityModel>()), Times.Exactly(action == MarketPositionAction.Close ? 1 : 0));
    }

    [Fact]
    public async Task Passes_the_decoded_event_to_the_service()
    {
        var model = MessagingTestData.Spread(action: MarketPositionAction.Update, symbol: "ETH/USDT");

        await Sut().HandleAsync(Message(model), CancellationToken.None);

        _spreads.Verify(s => s.UpdateSpread(It.Is<TradeOpportunityModel>(m => m.Guid == model.Guid && m.Symbol == "ETH/USDT")), Times.Once);
    }

    [Fact]
    public async Task A_second_delivery_of_the_same_message_id_has_no_side_effect()
    {
        var message = Message(MessagingTestData.Spread(action: MarketPositionAction.Open));

        await Sut().HandleAsync(message, CancellationToken.None);
        await Sut().HandleAsync(message, CancellationToken.None);

        _spreads.Verify(s => s.AddSpread(It.IsAny<TradeOpportunityModel>()), Times.Once);
        _store.Keys.Should().Equal("spread-dedupe:webapp:m-1");
    }

    [Fact]
    public async Task Distinct_message_ids_for_identical_content_are_distinct_events()
    {
        var model = MessagingTestData.Spread(action: MarketPositionAction.Update);

        await Sut().HandleAsync(Message(model, "m-1"), CancellationToken.None);
        await Sut().HandleAsync(Message(model, "m-2"), CancellationToken.None);

        _spreads.Verify(s => s.UpdateSpread(It.IsAny<TradeOpportunityModel>()), Times.Exactly(2));
    }

    [Fact]
    public async Task A_failed_result_is_thrown_so_the_message_is_retried_instead_of_silently_lost()
    {
        _spreads.Setup(s => s.AddSpread(It.IsAny<TradeOpportunityModel>())).ReturnsAsync(Result.Fail("mongo is down"));

        var act = () => Sut().HandleAsync(Message(MessagingTestData.Spread()), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*mongo is down*");
        _store.Keys.Should().BeEmpty("the claim is released so the retry can run the work again");
    }

    [Fact]
    public async Task A_retry_after_a_failure_applies_the_event()
    {
        var message = Message(MessagingTestData.Spread());
        _spreads.SetupSequence(s => s.AddSpread(It.IsAny<TradeOpportunityModel>()))
            .ReturnsAsync(Result.Fail("transient"))
            .ReturnsAsync(Result.Ok());

        var first = () => Sut().HandleAsync(message, CancellationToken.None);
        await first.Should().ThrowAsync<InvalidOperationException>();
        await Sut().HandleAsync(message, CancellationToken.None);

        _spreads.Verify(s => s.AddSpread(It.IsAny<TradeOpportunityModel>()), Times.Exactly(2));
        _store.Keys.Should().Equal("spread-dedupe:webapp:m-1");
    }

    [Fact]
    public async Task A_missing_message_id_is_non_retryable()
    {
        var act = () => Sut().HandleAsync(Message(MessagingTestData.Spread(), messageId: null), CancellationToken.None);

        await act.Should().ThrowAsync<NonRetryableMessageException>().WithMessage("*message-id*");
        _spreads.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task An_undecodable_payload_is_non_retryable()
    {
        var act = () => Sut().HandleAsync(Raw([0xFF, 0xFF, 0xFF, 0xFF, 0xFF], "m-1"), CancellationToken.None);

        await act.Should().ThrowAsync<NonRetryableMessageException>();
    }

    [Fact]
    public async Task An_unknown_action_type_is_non_retryable_and_does_not_claim_the_message()
    {
        var model = MessagingTestData.Spread();
        model.ActionType = (MarketPositionAction)42;

        var act = () => Sut().HandleAsync(Message(model), CancellationToken.None);

        await act.Should().ThrowAsync<NonRetryableMessageException>();
        _store.Keys.Should().BeEmpty();
    }
}
