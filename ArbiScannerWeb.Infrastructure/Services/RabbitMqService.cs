using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Messaging;
using ArbiScannerWeb.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading.Channels;
using ArbiScannerWeb.Domain.Models;
using ProtoBuf;
using ProtoBuf.Meta;
using ArbiScannerWeb.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using StackExchange.Redis;

namespace ArbiScannerWeb.Infrastructure.Services
{
   public class RabbitMqService : IRabbitMqService, IDisposable, IAsyncDisposable
    {
        public const string DeadLetterExchange = "spread_dlx";

        private static readonly TimeSpan DedupeWindow = TimeSpan.FromMinutes(15);

        private IConnection? _connection;
        private IChannel? _channel;
        private readonly ILogger<RabbitMqService> _logger;
        private AsyncEventingBasicConsumer? _consumer;
        private readonly string _queueName;
        private bool _isConsuming = false;
        private readonly IOptions<RabbitMqSettings> _settings;
        private readonly IDatabase _redis;
        private readonly ResiliencePipeline _retryPipeline;
        private readonly IParityRecorder _parity;
        private readonly IIdempotencyStore _idempotency;
        private readonly string _dedupeScope;
        private bool _disposed = false;
        public event Func<TradeOpportunityModel, Task>? OnMessageReceived;

        public RabbitMqService(
            ILogger<RabbitMqService> logger,
            IOptions<RabbitMqSettings> settings,
            IConnectionMultiplexer redis,
            IParityRecorder? parity = null,
            IOptions<MessagingOptions>? messaging = null)
        {
            _logger = logger;
            _settings = settings;
            _queueName = _settings.Value.Queue;
            _redis = redis.GetDatabase();
            _parity = parity ?? new NoOpParityRecorder();
            _idempotency = new RedisIdempotencyStore(redis);
            _dedupeScope = string.IsNullOrWhiteSpace(messaging?.Value.ServiceName) ? _queueName : messaging.Value.ServiceName;
            _retryPipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                })
                .Build();
        }

        // Hashes the fields that actually change tick-to-tick into a fixed-size fingerprint
        // instead of keying on the raw values, so the Redis key stays short and cheap to
        // store/compare no matter how many fields are checked for a genuine change.
        private static string BuildDedupeKey(string queueName, TradeOpportunityModel position)
        {
            var fingerprint = string.Create(CultureInfo.InvariantCulture, $"{position.Spread}|{position.ExchangeRateA.ExchangeRate}|{position.ExchangeRateA.VolumeAsk}|{position.ExchangeRateA.VolumeBid}|{position.ExchangeRateB.ExchangeRate}|{position.ExchangeRateB.VolumeAsk}|{position.ExchangeRateB.VolumeBid}|{position.PossibleProfit}|{position.SummaryTarrif}|{position.StartSpread}|{position.TotalFunding}");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));

            return $"spread-dedupe:{queueName}:{position.Guid}:{position.ActionType}:{hash}";
        }

        private async Task<bool> EnsureConnectedAsync()
        {
            if (_connection?.IsOpen == true && _channel?.IsOpen == true)
                return true;

            try
            {
                var factory = new ConnectionFactory()
                {
                    HostName = _settings.Value.Host,
                    Port = _settings.Value.Port ?? AmqpTcpEndpoint.UseDefaultPort,
                    UserName = _settings.Value.Username,
                    Password = _settings.Value.Password
                };

                _connection = await factory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();
                _logger.LogInformation("RabbitMQ connection established successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to establish RabbitMQ connection");
                return false;
            }
        }

        public async Task StartConsumingAsync(CancellationToken cancellationToken)
        {
            if (_isConsuming)
            {
                return;
            }

            if (!await EnsureConnectedAsync())
            {
                _logger.LogWarning("RabbitMQ channel/connection is not initialized, skipping consume");
                return;
            }

            try
            {
                var dlqQueueName = $"{_queueName}_dlq";

                await _channel!.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Fanout, durable: true, cancellationToken: cancellationToken);
                await _channel!.QueueDeclareAsync(dlqQueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
                await _channel!.QueueBindAsync(dlqQueueName, DeadLetterExchange, routingKey: "", cancellationToken: cancellationToken);

                await _channel!.ExchangeDeclareAsync(_settings.Value.Exchange, ExchangeType.Fanout, durable: true, cancellationToken: cancellationToken);
                await _channel!.QueueDeclareAsync(_queueName, durable: true, exclusive: false, autoDelete: false,
                    arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = DeadLetterExchange }, cancellationToken: cancellationToken);
                await _channel!.QueueBindAsync(_queueName, _settings.Value.Exchange, _settings.Value.RoutingKey ?? "", cancellationToken: cancellationToken);
                _consumer = new AsyncEventingBasicConsumer(_channel!);
                _consumer.ReceivedAsync += async (model, ea) =>
                {
                    TradeOpportunityModel? position = null;
                    try
                    {
                        position = TradeOpportunitySerializer.Deserialize(ea.Body.ToArray());

                        _logger.LogInformation("Received message: {Message}", position.Spread);

                        var messageId = ea.BasicProperties.IsMessageIdPresent() ? ea.BasicProperties.MessageId : null;
                        await RecordParityAsync(position, messageId, ea.BasicProperties);

                        if (string.IsNullOrEmpty(messageId))
                        {
                            await ProcessWithContentHashDedupeAsync(position, ea);
                        }
                        else
                        {
                            await ProcessWithMessageIdDedupeAsync(position, messageId, ea);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message, sending to dead-letter queue");
                        await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: CancellationToken.None);
                    }
                };

                await _channel!.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: _consumer, cancellationToken: cancellationToken);
                _isConsuming = true;

                _logger.LogInformation("Started consuming messages from queue: {QueueName}", _queueName);
            }
            catch (Exception ex)
            {
                _isConsuming = false;
                _connection = null;
                _channel = null;
                _logger.LogError(ex, "Failed to start consuming messages");
            }
        }

        // Messages published by a scanner that stamps a message-id: dedupe on (service, message-id) with the same two-state
        // store the Kafka consumer uses, so a redelivery after a crash is retried (not skipped) and both brokers agree on what
        // has already been processed.
        private async Task ProcessWithMessageIdDedupeAsync(TradeOpportunityModel position, string messageId, BasicDeliverEventArgs ea)
        {
            var ran = await IdempotentExecutor.RunOnceAsync(
                _idempotency,
                $"spread-dedupe:{_dedupeScope}:{messageId}",
                _ => _retryPipeline.ExecuteAsync(async _ => await InvokeHandlerAsync(position)).AsTask(),
                maxWait: TimeSpan.FromSeconds(90),
                pollInterval: TimeSpan.FromSeconds(1),
                CancellationToken.None);

            if (!ran)
            {
                _logger.LogInformation("Duplicate delivery skipped: message-id {MessageId}", messageId);
            }

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
        }

        // Legacy messages without a message-id: content-hash dedupe, unchanged.
        private async Task ProcessWithContentHashDedupeAsync(TradeOpportunityModel position, BasicDeliverEventArgs ea)
        {
            var dedupeKey = BuildDedupeKey(_queueName, position);
            var claimed = await _redis.StringSetAsync(dedupeKey, "1", DedupeWindow, When.NotExists);
            if (!claimed)
            {
                _logger.LogInformation("Duplicate delivery skipped: {DedupeKey}", dedupeKey);
                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
                return;
            }

            try
            {
                await _retryPipeline.ExecuteAsync(async _ => await InvokeHandlerAsync(position));

                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
            }
            catch (Exception)
            {
                await _redis.KeyDeleteAsync(dedupeKey);
                throw;
            }
        }

        private async Task InvokeHandlerAsync(TradeOpportunityModel position)
        {
            if (OnMessageReceived != null)
            {
                await OnMessageReceived.Invoke(position);
            }
        }

        // Parity bookkeeping must never affect delivery: any failure here is logged and swallowed.
        private async Task RecordParityAsync(TradeOpportunityModel position, string? messageId, IReadOnlyBasicProperties properties)
        {
            if (string.IsNullOrEmpty(messageId))
            {
                return;
            }

            try
            {
                var producedAt = properties.IsTimestampPresent()
                    ? DateTimeOffset.FromUnixTimeSeconds(properties.Timestamp.UnixTime)
                    : DateTimeOffset.UtcNow;

                await _parity.RecordAsync(MessagingBackends.RabbitMq, SpreadEventKeys.For(position), messageId, producedAt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record RabbitMQ parity for message {MessageId}", messageId);
            }
        }

        public async Task StopConsumingAsync()
        {
            if (!_isConsuming) return;

            try
            {
                if (_channel != null && _consumer?.ConsumerTags?.FirstOrDefault() is string tag)
                    await _channel.BasicCancelAsync(tag, cancellationToken: CancellationToken.None);
                _isConsuming = false;
                _logger.LogInformation("Stopped consuming messages");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping message consumption");
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                StopConsumingAsync().Wait();
                _channel?.CloseAsync().Wait();
                _channel?.Dispose();
                _connection?.CloseAsync().Wait();
                _connection?.Dispose();
            }

            _disposed = true;
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_disposed) return;

            await StopConsumingAsync();
            if (_channel != null)
            {
                await _channel.CloseAsync();
                _channel.Dispose();
            }
            if (_connection != null)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
            }

            _disposed = true;
        }

        ~RabbitMqService()
        {
            Dispose(disposing: false);
        }
    }
}
