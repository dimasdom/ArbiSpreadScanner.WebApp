using ArbiScannerWeb.Messaging.Kafka;
using FluentAssertions;

namespace ArbiScannerWeb.Tests.Messaging;

public class HandlerRetryPolicyTests
{
    private static HandlerRetryPolicy Policy(int attempts = 3) =>
        new(attempts, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500));

    [Theory]
    [InlineData(1, RetryDecision.Retry)]
    [InlineData(2, RetryDecision.Retry)]
    [InlineData(3, RetryDecision.DeadLetter)]
    [InlineData(4, RetryDecision.DeadLetter)]
    public void Decide_retries_until_the_attempt_budget_is_spent(int failedAttempt, RetryDecision expected)
    {
        Policy().Decide(failedAttempt, new InvalidOperationException()).Should().Be(expected);
    }

    [Fact]
    public void Decide_dead_letters_non_retryable_failures_immediately()
    {
        Policy().Decide(1, new NonRetryableMessageException("bad payload")).Should().Be(RetryDecision.DeadLetter);
    }

    [Fact]
    public void Decide_with_a_single_attempt_never_retries()
    {
        Policy(attempts: 1).Decide(1, new InvalidOperationException()).Should().Be(RetryDecision.DeadLetter);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 200)]
    [InlineData(3, 400)]
    [InlineData(4, 500)]
    [InlineData(40, 500)]
    public void DelayAfter_backs_off_exponentially_up_to_the_cap(int failedAttempt, int expectedMs)
    {
        Policy().DelayAfter(failedAttempt).Should().Be(TimeSpan.FromMilliseconds(expectedMs));
    }

    [Fact]
    public void From_reads_the_consumer_options()
    {
        var policy = HandlerRetryPolicy.From(new KafkaConsumerOptions { MaxHandlerAttempts = 2, RetryBaseDelayMs = 10, RetryMaxDelayMs = 20 });

        policy.Decide(2, new InvalidOperationException()).Should().Be(RetryDecision.DeadLetter);
        policy.DelayAfter(5).Should().Be(TimeSpan.FromMilliseconds(20));
    }

    [Fact]
    public void Constructor_rejects_invalid_arguments()
    {
        var zeroAttempts = () => new HandlerRetryPolicy(0, TimeSpan.Zero, TimeSpan.Zero);
        var maxBelowBase = () => new HandlerRetryPolicy(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));

        zeroAttempts.Should().Throw<ArgumentOutOfRangeException>();
        maxBelowBase.Should().Throw<ArgumentOutOfRangeException>();
    }
}
