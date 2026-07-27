using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArbiScannerWeb.IntegrationTests.Support;

internal sealed class CustomWebApplicationFactory(
    IReadOnlyDictionary<string, string?> configOverrides,
    Action<IServiceCollection>? configureTestServices = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(configOverrides);
        });

        if (configureTestServices is not null)
        {
            builder.ConfigureTestServices(configureTestServices);
        }
    }

    // Auth cookies are always issued with Secure = true (never conditioned on the inbound
    // request's scheme) - see AccountController.CreateCookieOptions. .NET's CookieContainer
    // honors that flag client-side too: it will only attach/re-send a Secure cookie on a
    // connection it considers HTTPS. CreateClient()'s default BaseAddress is http://localhost,
    // and WebApplicationFactory.CreateClient(WebApplicationFactoryClientOptions) unconditionally
    // resets BaseAddress from the passed-in options after ConfigureClient runs (that override
    // point is not enough - it gets clobbered), so the only reliable fix is to hand it an
    // options instance with an https BaseAddress up front. Use this instead of CreateClient()
    // in any integration test that needs the login/refresh cookies to round-trip.
    internal HttpClient CreateSecureClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
}
