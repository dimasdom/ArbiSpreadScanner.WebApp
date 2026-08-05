using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.IntegrationTests.Fixtures;
using ArbiScannerWeb.IntegrationTests.Support;
using FluentAssertions;

namespace ArbiScannerWeb.IntegrationTests.Api;

// The one test in the suite that proves the API's resource-server config
// actually works against a live Keycloak instance: real Authority-based OIDC
// discovery, real JWKS signature validation, real realm import of the exact
// file that ships to production. Every other test uses the fast forged-JWT
// path (JwtTestSettings/JwtTestTokenFactory) instead.
[Collection(KeycloakCollection.Name)]
public class KeycloakDiscoveryTests(KeycloakTestFixture fixture)
{
    [Fact]
    public async Task GetUserData_WithRealKeycloakToken_JitProvisionsUserAndReturnsData()
    {
        var token = await fixture.GetServiceAccountTokenAsync();
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/Account/GetUserData");
        var result = await response.Content.ReadFromJsonAsync<ApiResult<AccountDto>>(JsonOptions.CaseInsensitive);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserData_WithoutToken_ReturnsUnauthorized()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/Account/GetUserData");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
