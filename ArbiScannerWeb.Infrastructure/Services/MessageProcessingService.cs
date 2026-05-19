using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Infrastructure.Services
{
    public class MessageProcessingService : BackgroundService
    {
        private readonly IRabbitMqService _rabbitMqService;
        private readonly ILogger<MessageProcessingService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public MessageProcessingService(
            IRabbitMqService rabbitMqService,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<MessageProcessingService> logger)
        {
            _rabbitMqService = rabbitMqService;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Message Processing Service started");

            // Subscribe to RabbitMQ message events
            _rabbitMqService.OnMessageReceived += ProcessMessageAsync;

            stoppingToken.Register(() => _logger.LogInformation("Cancellation requested"));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {

                    await _rabbitMqService.StartConsumingAsync(stoppingToken);
                    _logger.LogInformation("Started consuming from RabbitMQ");

                    // Wait and reconnect every 10 seconds
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (Exception ex)
                {
                    await _rabbitMqService.StopConsumingAsync();
                    _logger.LogError(ex, "RabbitMQ consuming failed. Will retry in 5 seconds");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            try
            {
                await _rabbitMqService.StopConsumingAsync();
                _logger.LogInformation("Stopped consuming from RabbitMQ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping RabbitMQ consumer");
            }
        }

        private Task ProcessMessageAsync(TradeOpportunityModel possiblePosition)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var spreadService = scope.ServiceProvider.GetRequiredService<ITradeOpportunityService>();

                    switch (possiblePosition.ActionType)
                    {
                        case MarketPositionAction.Open:
                            await spreadService.AddSpread(possiblePosition);
                            break;
                        case MarketPositionAction.Update:
                            await spreadService.UpdateSpread(possiblePosition);
                            break;
                        case MarketPositionAction.Close:
                            await spreadService.CloseSpread(possiblePosition);
                            break;
                        default:
                            _logger.LogWarning("Unknown action type: {ActionType}", possiblePosition.ActionType);
                            return;
                    }

                    _logger.LogInformation("Processed message: {ActionType}", possiblePosition.ActionType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing RabbitMQ message");
                }
            });

            return Task.CompletedTask;
        }
    }
}
