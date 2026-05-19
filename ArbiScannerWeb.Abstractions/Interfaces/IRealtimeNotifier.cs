namespace ArbiScannerWeb.Abstractions.Interfaces
{
    /// <summary>
    /// Transport-agnostic abstraction for pushing real-time notifications to connected clients.
    /// </summary>
    public interface IRealtimeNotifier
    {
        Task NotifyGroupAsync(string group, object payload);
    }
}
