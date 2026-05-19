using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.API.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ArbiScannerWeb.API.Services
{
    public class SignalRService : IRealtimeNotifier
    {
        private readonly IHubContext<TradeOpportunityHub> _hubContext;
        private readonly ILogger<SignalRService> _logger;

        public SignalRService(IHubContext<TradeOpportunityHub> hubContext, ILogger<SignalRService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyGroupAsync(string group, object payload)
        {
            try
            {
                await _hubContext.Clients.Group(group).SendAsync("ReceiveMessage", payload);
                _logger.LogInformation("Message sent to group {GroupName}", group);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to group {GroupName}", group);
                throw;
            }
        }
    }
}
