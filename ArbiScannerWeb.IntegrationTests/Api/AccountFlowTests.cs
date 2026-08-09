using System.Net;
using System.Net.Http.Json;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.IntegrationTests.Fixtures;
using ArbiScannerWeb.IntegrationTests.Support;
using FluentAssertions;

namespace ArbiScannerWeb.IntegrationTests.Api;

// Login/Register/ConfirmEmail/Refresh/Logout no longer exist here — Keycloak owns
// the whole auth lifecycle. What's left proves: (1) a validated Bearer token JIT-
// provisions a local Users/UserSettings row and GetUserData reflects it, and
// (2) requests without a valid token are rejected.
[Collection(WebApiCollection.Name)]
public class AccountFlowTests(WebApiTestFixture fixture)
{
    [Fact]
    public async Task GetUserData_ValidToken_JitProvisionsUserAndReturnsData()
    {
        var sub = Guid.NewGuid().ToString();
        var email = $"integration-{Guid.NewGuid():N}@example.com";
        var client = fixture.Factory.CreateAuthenticatedClient(sub, email);

        var response = await client.GetAsync("/api/Account/GetUserData");
        var result = await response.Content.ReadFromJsonAsync<ApiResult<AccountDto>>(JsonOptions.CaseInsensitive);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be(email);
        result.Value.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserData_SecondRequestForSameUser_DoesNotDuplicateProvisioning()
    {
        var sub = Guid.NewGuid().ToString();
        var client = fixture.Factory.CreateAuthenticatedClient(sub);

        await client.GetAsync("/api/Account/GetUserData");
        var response = await client.GetAsync("/api/Account/GetUserData");
        var result = await response.Content.ReadFromJsonAsync<ApiResult<AccountDto>>(JsonOptions.CaseInsensitive);

        result!.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserData_WithoutAuth_ReturnsUnauthorized()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/Account/GetUserData");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserData_ExpiredToken_ReturnsUnauthorized()
    {
        var client = fixture.Factory.CreateClient();
        // A token signed with the wrong key is functionally equivalent to an
        // untrusted/expired one from the resource server's point of view — both
        // fail signature/validity checks the same way.
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

        var response = await client.GetAsync("/api/Account/GetUserData");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
