using System.Net;
using System.Net.Http.Json;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.LoadTests.Settings;

namespace ArbiScannerWeb.LoadTests.Support;

internal sealed record AuthenticatedSession(HttpClient Client, AccountDto Account);

internal static class AuthenticatedClientFactory
{
    public static async Task<AuthenticatedSession> CreateAsync(LoadTestSettings settings)
    {
        var handler = new SocketsHttpHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = LoadRunner.MaxConcurrency(settings.QueriesPerMinute)
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(settings.BaseUrl)
        };

        var loginResponse = await client.PostAsJsonAsync("/api/Account/Login", new AccountLoginDto
        {
            Login = settings.Email,
            Password = settings.Password
        });

        loginResponse.EnsureSuccessStatusCode();

        var result = await loginResponse.Content.ReadFromJsonAsync<ApiResult<AccountDto>>(JsonOptions.CaseInsensitive);
        if (result is not { IsSuccess: true } || result.Value is not { EmailConfirmed: true })
        {
            client.Dispose();
            throw new InvalidOperationException(
                $"Load test login failed for '{settings.Email}' at '{settings.BaseUrl}': {result?.Message ?? "no response body"}");
        }

        return new AuthenticatedSession(client, result.Value);
    }
}
