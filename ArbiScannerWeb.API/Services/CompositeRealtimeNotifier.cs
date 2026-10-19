using ArbiScannerWeb.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace ArbiScannerWeb.API.Services
{
    /// <summary>Temporary: publishes to the SignalR hub and the SSE broker at once, so the SPA keeps working while it is moved
    /// from one to the other. Removed together with the hub. One failing transport never blocks the other.</summary>
    public class CompositeRealtimeNotifier(IEnumerable<IRealtimeNotifier> notifiers, ILogger<CompositeRealtimeNotifier> logger) : IRealtimeNotifier
    {
        private readonly IRealtimeNotifier[] _notifiers = [.. notifiers];

        public async Task NotifyGroupAsync(string group, object payload)
        {
            foreach (var notifier in _notifiers)
            {
                try
                {
                    await notifier.NotifyGroupAsync(group, payload);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{Notifier} failed to notify group {GroupName}", notifier.GetType().Name, group);
                }
            }
        }
    }
}
