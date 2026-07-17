using ArbiScannerWeb.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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

namespace ArbiScannerWeb.Infrastructure.Services
{
   public class RabbitMqService : IRabbitMqService, IDisposable, IAsyncDisposable
    {
        private IConnection? _connection;
        private IChannel? _channel;
        private readonly ILogger<RabbitMqService> _logger;
        private AsyncEventingBasicConsumer? _consumer;
        private readonly string _queueName;
        private bool _isConsuming = false;
        private readonly IOptions<RabbitMqSettings> _settings;
        private bool _disposed = false;
        private static readonly RuntimeTypeModel ProtobufModel = CreateProtobufModel();
        public event Func<TradeOpportunityModel, Task>? OnMessageReceived;

        public RabbitMqService(ILogger<RabbitMqService> logger, IOptions<RabbitMqSettings> settings)
        {
            _logger = logger;
            _settings = settings;
            _queueName = _settings.Value.Queue;
        }

        private static RuntimeTypeModel CreateProtobufModel()
        {
            var model = RuntimeTypeModel.Create();

            model.Add(typeof(ExchangeRateModel), true);

            var orderLevelMeta = model.Add(typeof(OrderLevelModel), false);
            orderLevelMeta.Add(1, nameof(OrderLevelModel.Price));
            orderLevelMeta.Add(2, nameof(OrderLevelModel.Amount));

            var tradeOpportunityMeta = model.Add(typeof(TradeOpportunityModel), false);
            tradeOpportunityMeta.Add(1, nameof(TradeOpportunityModel.Guid));
            tradeOpportunityMeta.Add(2, nameof(TradeOpportunityModel.ExchangeRateA));
            tradeOpportunityMeta.Add(3, nameof(TradeOpportunityModel.ExchangeRateB));
            tradeOpportunityMeta.Add(4, nameof(TradeOpportunityModel.ExchangeShort));
            tradeOpportunityMeta.Add(5, nameof(TradeOpportunityModel.ExchangeLong));
            tradeOpportunityMeta.Add(7, nameof(TradeOpportunityModel.SummaryTarrif));
            tradeOpportunityMeta.Add(8, nameof(TradeOpportunityModel.PossibleProfit));
            tradeOpportunityMeta.Add(9, nameof(TradeOpportunityModel.TotalFunding));
            tradeOpportunityMeta.Add(10, nameof(TradeOpportunityModel.Spread));
            tradeOpportunityMeta.Add(11, nameof(TradeOpportunityModel.Type));
            tradeOpportunityMeta.Add(12, nameof(TradeOpportunityModel.ActionType));
            tradeOpportunityMeta.Add(13, nameof(TradeOpportunityModel.StartSpread));
            tradeOpportunityMeta.Add(14, nameof(TradeOpportunityModel.Symbol));
            tradeOpportunityMeta.Add(15, nameof(TradeOpportunityModel.BidsExchangeA));
            tradeOpportunityMeta.Add(16, nameof(TradeOpportunityModel.AsksExchangeA));
            tradeOpportunityMeta.Add(17, nameof(TradeOpportunityModel.BidsExchangeB));
            tradeOpportunityMeta.Add(18, nameof(TradeOpportunityModel.AsksExchangeB));
            tradeOpportunityMeta.Add(19, nameof(TradeOpportunityModel.DateTime));

            return model;
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
                await _channel!.ExchangeDeclareAsync(_settings.Value.Exchange, ExchangeType.Fanout, durable: true);
                await _channel!.QueueDeclareAsync(_queueName, durable: false, exclusive: false, autoDelete: false);
                await _channel!.QueueBindAsync(_queueName, _settings.Value.Exchange, _settings.Value.RoutingKey ?? "");
                _consumer = new AsyncEventingBasicConsumer(_channel!);
                _consumer.ReceivedAsync += async (model, ea) =>
                {
                    try
                    {
                        using var stream = new MemoryStream(ea.Body.ToArray());
                        var position = (TradeOpportunityModel)ProtobufModel.Deserialize(
                            stream,
                            value: null,
                            type: typeof(TradeOpportunityModel));

                        _logger.LogInformation("Received message: {Message}", position.Spread);

                        if (OnMessageReceived != null)
                        {
                            var _ = OnMessageReceived.Invoke(position);
                        }

                        await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message");
                        await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                    }
                };

                await _channel!.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: _consumer);
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

        public async Task StopConsumingAsync()
        {
            if (!_isConsuming) return;

            try
            {
                if (_channel != null && _consumer?.ConsumerTags?.FirstOrDefault() is string tag)
                    await _channel.BasicCancelAsync(tag);
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
