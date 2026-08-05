using ArbiScannerWeb.Abstractions.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Infrastructure.Middleware;

// Runs right after UseAuthentication() so it fronts both MVC controllers and the
// SignalR hub's connection/negotiate requests — an action filter would miss the hub.
public class JitUserProvisioningMiddleware
{
    private readonly RequestDelegate _next;

    public JitUserProvisioningMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IJitUserProvisioningService provisioningService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await provisioningService.EnsureProvisionedAsync(context.User);
        }

        await _next(context);
    }
}
