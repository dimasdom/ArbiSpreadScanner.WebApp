using System.Globalization;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArbiScannerWeb.Messaging.Kafka;

/// <summary>
/// One consumer-group member per host. Subscribes (never assigns), processes messages sequentially so
/// per-partition order is preserved, commits offsets only after processing and in batches, retries a failing
/// message a bounded number of times and then dead-letters it so a poison message can never block its partition.
/// </summary>
/// <remarks>
/// The consumers in this system keep no per-key in-memory state, so the revoke handler only commits what was
/// already processed; there is nothing to rebuild on assignment.
/// </remarks>
public sealed class KafkaConsumerService : BackgroundService
{
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxDeadLetterRetryDelay = TimeSpan.FromSeconds(10);

    private readonly KafkaOptions _kafka;
    private readonly KafkaConsumerOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDeadLetterPublisher _deadLetters;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly HandlerRetryPolicy _retryPolicy;
    private readonly TimeProvider _clock;

    public KafkaConsumerService(
        IOptions<KafkaOptions> kafka,
        IOptions<KafkaConsumerOptions> options,
        IServiceScopeFactory scopeFactory,
        IDeadLetterPublisher deadLetters,
        ILogger<KafkaConsumerService> logger,
        TimeProvider? clock = null)
    {
        _kafka = kafka.Value;
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _deadLetters = deadLetters;
        _logger = logger;
        _retryPolicy = HandlerRetryPolicy.From(_options);
        _clock = clock ?? TimeProvider.System;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Kafka consumer is disabled (Kafka:Consumer:Enabled=false)");
            return Task.CompletedTask;
        }

        return Task.Factory.StartNew(() => RunAsync(stoppingToken), stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kafka consumer for group {GroupId} failed; restarting in {Delay}", _options.GroupId, RestartDelay);
                try
                {
                    await Task.Delay(RestartDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var tracker = new CommitTracker(_options.CommitBatchSize, TimeSpan.FromSeconds(_options.CommitIntervalSeconds), _clock);

        using var consumer = new ConsumerBuilder<string, byte[]>(BuildConfig())
            .SetPartitionsAssignedHandler((_, partitions) =>
                _logger.LogInformation("Kafka partitions assigned: {Partitions}", string.Join(',', partitions)))
            .SetPartitionsRevokedHandler((c, partitions) =>
            {
                _logger.LogInformation("Kafka partitions revoked: {Partitions}", string.Join(',', partitions.Select(p => p.TopicPartition)));
                CommitPending(c, tracker);
                tracker.Forget(partitions.Select(p => p.TopicPartition));
            })
            .SetPartitionsLostHandler((_, partitions) =>
            {
                _logger.LogWarning("Kafka partitions lost: {Partitions}", string.Join(',', partitions.Select(p => p.TopicPartition)));
                tracker.Forget(partitions.Select(p => p.TopicPartition));
            })
            .SetErrorHandler((_, error) =>
                _logger.LogWarning("Kafka consumer error: {Reason} (fatal={Fatal})", error.Reason, error.IsFatal))
            .Build();

        consumer.Subscribe(_options.Topic);
        _logger.LogInformation("Kafka consumer started. Group={GroupId} Topic={Topic}", _options.GroupId, _options.Topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, byte[]>? result;
                try
                {
                    result = consumer.Consume(TimeSpan.FromMilliseconds(_options.PollTimeoutMs));
                }
                catch (ConsumeException ex) when (!ex.Error.IsFatal)
                {
                    _logger.LogWarning(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
                    continue;
                }

                if (result is not null)
                {
                    await ProcessAsync(result, stoppingToken);
                    tracker.Record(result.TopicPartitionOffset);
                }

                if (tracker.ShouldCommit())
                {
                    CommitPending(consumer, tracker);
                }
            }
        }
        finally
        {
            // Release partitions cleanly so the group rebalances immediately instead of waiting for the session timeout.
            CommitPending(consumer, tracker);
            consumer.Close();
            _logger.LogInformation("Kafka consumer stopped. Group={GroupId}", _options.GroupId);
        }
    }

    private ConsumerConfig BuildConfig() => new()
    {
        BootstrapServers = _kafka.BootstrapServers,
        ClientId = _kafka.ClientId,
        GroupId = _options.GroupId,
        EnableAutoCommit = false,
        AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_options.AutoOffsetReset, ignoreCase: true),
        MaxPollIntervalMs = _options.MaxPollIntervalMs,
        SessionTimeoutMs = _options.SessionTimeoutMs,
    };

    private void CommitPending(IConsumer<string, byte[]> consumer, CommitTracker tracker)
    {
        var offsets = tracker.Drain();
        if (offsets.Count == 0)
        {
            return;
        }

        try
        {
            consumer.Commit(offsets);
        }
        catch (KafkaException ex)
        {
            // Not fatal: the uncommitted tail is redelivered and suppressed by the idempotency store.
            _logger.LogWarning(ex, "Kafka commit failed for {Offsets}; messages will be redelivered", string.Join(',', offsets));
        }
    }

    private async Task ProcessAsync(ConsumeResult<string, byte[]> result, CancellationToken stoppingToken)
    {
        var message = ToMessage(result);

        using var activity = KafkaTelemetry.StartConsumer(message, _options.GroupId);
        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = activity?.TraceId.ToString(),
            ["Topic"] = message.Topic,
            ["Partition"] = message.Partition,
            ["Offset"] = message.Offset,
            ["Key"] = message.Key,
            ["MessageId"] = message.Metadata.MessageId,
        });

        var (failure, attempts) = await TryHandleAsync(message, stoppingToken);
        if (failure is null)
        {
            return;
        }

        KafkaTelemetry.RecordFailure(activity, failure);
        _logger.LogError(failure, "Message failed after {Attempts} attempt(s); dead-lettering", attempts);
        await DeadLetterAsync(message, failure, attempts, stoppingToken);
    }

    private async Task<(Exception? Failure, int Attempts)> TryHandleAsync(ConsumedMessage message, CancellationToken stoppingToken)
    {
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<IKafkaMessageHandler>();
                await handler.HandleAsync(message, stoppingToken);
                return (null, attempt);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (_retryPolicy.Decide(attempt, ex) == RetryDecision.DeadLetter)
                {
                    return (ex, attempt);
                }

                var delay = _retryPolicy.DelayAfter(attempt);
                _logger.LogWarning(ex, "Message handler failed (attempt {Attempt}); retrying in {Delay}", attempt, delay);
                await Task.Delay(delay, _clock, stoppingToken);
            }
        }
    }

    /// <summary>Retries until the dead-letter write is acknowledged: dropping the message silently would be worse
    /// than waiting, and if Kafka itself is down nothing could be consumed anyway.</summary>
    private async Task DeadLetterAsync(ConsumedMessage message, Exception failure, int attempts, CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (true)
        {
            try
            {
                await _deadLetters.PublishAsync(message, failure, attempts, stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dead-letter publish failed; retrying in {Delay}", delay);
                await Task.Delay(delay, _clock, stoppingToken);
                delay = delay * 2 > MaxDeadLetterRetryDelay ? MaxDeadLetterRetryDelay : delay * 2;
            }
        }
    }

    private static ConsumedMessage ToMessage(ConsumeResult<string, byte[]> result) => new(
        result.Topic,
        result.Partition.Value,
        result.Offset.Value,
        result.Message.Key ?? string.Empty,
        result.Message.Value ?? [],
        result.Message.Headers ?? [],
        MessageHeaders.Read(result.Message.Headers),
        result.Message.Timestamp.UtcDateTime);
}
