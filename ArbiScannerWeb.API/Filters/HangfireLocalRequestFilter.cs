using Hangfire.Dashboard;

namespace ArbiScannerWeb.API.Filters;

public sealed class HangfireLocalRequestFilter : IDashboardAuthorizationFilter
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar", "S2325", Justification = "Interface implementation cannot be static")]
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        return (http.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip));
    }
}
