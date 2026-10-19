namespace ArbiScannerWeb.Messaging.Kafka;

public enum RetryDecision
{
    Retry,
    DeadLetter,
}

/// <summary>Pure decision logic for bounded retries: how many attempts, how long to back off, and when a
/// failing message is given up on and sent to the dead-letter topic.</summary>
public sealed class HandlerRetryPolicy
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;

    public HandlerRetryPolicy(int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(baseDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDelay, baseDelay);

        _maxAttempts = maxAttempts;
        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
    }

    public static HandlerRetryPolicy From(KafkaConsumerOptions options) => new(
        options.MaxHandlerAttempts,
        TimeSpan.FromMilliseconds(options.RetryBaseDelayMs),
        TimeSpan.FromMilliseconds(options.RetryMaxDelayMs));

    /// <param name="failedAttempt">1-based number of the attempt that just failed.</param>
    public RetryDecision Decide(int failedAttempt, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error is NonRetryableMessageException || failedAttempt >= _maxAttempts)
        {
            return RetryDecision.DeadLetter;
        }

        return RetryDecision.Retry;
    }

    /// <summary>Exponential backoff: base, 2×base, 4×base, ... capped at the maximum delay.</summary>
    public TimeSpan DelayAfter(int failedAttempt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failedAttempt, 1);

        var factor = Math.Pow(2, Math.Min(failedAttempt - 1, 30));
        var delay = TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * factor);
        return delay > _maxDelay ? _maxDelay : delay;
    }
}
