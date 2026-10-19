using ArbiScannerWeb.Abstractions.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Infrastructure.Middleware;

// Runs right after UseAuthentication() so it fronts every authenticated endpoint — MVC controllers
// and the spread SSE stream alike — which an action filter would not.
public class JitUserProvisioningMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IJitUserProvisioningService provisioningService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await provisioningService.EnsureProvisionedAsync(context.User);
        }

        await next(context);
    }
}
