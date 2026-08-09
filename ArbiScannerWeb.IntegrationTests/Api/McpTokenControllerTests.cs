using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArbiScannerWeb.IntegrationTests.Fixtures;
using ArbiScannerWeb.IntegrationTests.Support;
using FluentAssertions;

namespace ArbiScannerWeb.IntegrationTests.Api;

// Proves the MCP token generation feature works against a real Keycloak: a real
// Standard Token Exchange (RFC 8693) call succeeds against the actual
// arbiscanner-mcp client shipped in the production realm export, and the
// resulting token is both long-lived (the client's access.token.lifespan
// override, 30 days) and genuinely usable as a Bearer credential against the
// real API — exactly what ArbiScanner.McpServer forwards downstream as-is.
[Collection(KeycloakCollection.Name)]
public class McpTokenControllerTests(KeycloakTestFixture fixture)
{
    [Fact]
    public async Task Generate_WithoutToken_ReturnsUnauthorized()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsync("/api/McpToken/Generate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Generate_WithTokenCarryingMcpAudience_ReturnsALongLivedUsableAccessToken()
    {
        var subjectToken = await fixture.GetServiceAccountTokenAsync();
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", subjectToken);

        var response = await client.PostAsync("/api/McpToken/Generate", content: null);
        var result = await response.Content.ReadFromJsonAsync<ApiResult<string>>(JsonOptions.CaseInsensitive);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();

        // The client's access.token.lifespan override (2592000s / 30 days, see
        // keycloak/realm-export/arbiscanner-web-realm.json) must actually apply to
        // an exchanged token, not just a normally-issued one — confirmed here by
        // decoding the real JWT's own exp claim rather than trusting expires_in.
        DecodeJwtExpiry(result.Value).Should().BeAfter(DateTimeOffset.UtcNow.AddDays(29));

        // Prove it's genuinely usable, not just well-formed: call a real protected
        // endpoint with it as the Bearer, exactly what ArbiScanner.McpServer's
        // SpreadsApiClient does when it forwards this same token as-is.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Value);
        var secondResponse = await client.GetAsync("/api/Account/GetUserData");
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static DateTimeOffset DecodeJwtExpiry(string jwt)
    {
        var payloadSegment = jwt.Split('.')[1];
        var padded = payloadSegment.PadRight(payloadSegment.Length + (4 - payloadSegment.Length % 4) % 4, '=');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/')));
        var exp = JsonDocument.Parse(json).RootElement.GetProperty("exp").GetInt64();
        return DateTimeOffset.FromUnixTimeSeconds(exp);
    }
}
