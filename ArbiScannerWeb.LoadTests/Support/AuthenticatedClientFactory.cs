using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.LoadTests.Settings;

namespace ArbiScannerWeb.LoadTests.Support;

internal sealed record AuthenticatedSession(HttpClient Client, AccountDto Account);

// Login is Keycloak's job now, not this API's — obtains a token via the
// Resource Owner Password Credentials grant against the dedicated
// arbiscanner-web-loadtest client (direct-grant enabled, unlike the
// browser-facing SPA client, which stays PKCE-only). See
// keycloak/realm-export/arbiscanner-web-realm.json.
internal static class AuthenticatedClientFactory
{
    public static async Task<AuthenticatedSession> CreateAsync(LoadTestSettings settings)
    {
        var accessToken = await RequestAccessTokenAsync(settings);

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = LoadRunner.MaxConcurrency(settings.QueriesPerMinute)
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(settings.BaseUrl)
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userDataResponse = await client.GetAsync("/api/Account/GetUserData");
        userDataResponse.EnsureSuccessStatusCode();
        var result = await userDataResponse.Content.ReadFromJsonAsync<ApiResult<AccountDto>>(JsonOptions.CaseInsensitive);
        if (result is not { IsSuccess: true, Value: not null })
        {
            client.Dispose();
            throw new InvalidOperationException(
                $"Load test GetUserData failed for '{settings.Email}' at '{settings.BaseUrl}': {result?.Message ?? "no response body"}");
        }

        return new AuthenticatedSession(client, result.Value);
    }

    private static async Task<string> RequestAccessTokenAsync(LoadTestSettings settings)
    {
        using var authClient = new HttpClient();
        using var response = await authClient.PostAsync(
            $"{settings.OidcAuthority}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = settings.OidcClientId,
                ["username"] = settings.Email,
                ["password"] = settings.Password,
            }));

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Load test login failed for '{settings.Email}' against '{settings.OidcAuthority}': {(int)response.StatusCode} {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("access_token").GetString()!;
    }
}
