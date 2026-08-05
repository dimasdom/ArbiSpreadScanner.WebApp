using System.Net.Http.Headers;

namespace ArbiScannerWeb.IntegrationTests.Support;

// Replaces the old register->confirm-email->login cookie dance (Keycloak owns that
// flow now, not this API) with a forged Bearer token. The first authenticated
// request against a fresh sub JIT-provisions the local Users/UserSettings row.
internal static class AuthenticatedClientFactory
{
    public static HttpClient CreateAuthenticatedClient(this CustomWebApplicationFactory factory, string? sub = null, string? email = null)
    {
        sub ??= Guid.NewGuid().ToString();
        email ??= $"integration-{Guid.NewGuid():N}@example.com";

        var client = factory.CreateClient();
        var token = JwtTestTokenFactory.CreateToken(sub, email, preferredUsername: email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
