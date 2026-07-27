using System.Net;
using System.Net.Http.Json;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.IntegrationTests.Fixtures;
using ArbiScannerWeb.IntegrationTests.Support;
using FluentAssertions;

namespace ArbiScannerWeb.IntegrationTests.Api;

[Collection(WebApiCollection.Name)]
public class AccountFlowTests(WebApiTestFixture fixture)
{
    [Fact]
    public async Task FullAuthLifecycle_Succeeds()
    {
        // Uses CreateSecureClient(): the access/refresh cookies are Secure, so the client's
        // CookieContainer only re-sends them on a base address it considers HTTPS.
        var client = fixture.Factory.CreateSecureClient();
        var email = $"integration-{Guid.NewGuid():N}@example.com";
        const string password = "IntegrationTest@123";

        var registerResponse = await client.PostAsJsonAsync("/api/Account/Register", new AccountLoginDto { Login = email, Password = password });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<ApiResult<EmailConfirmationCodes>>(JsonOptions.CaseInsensitive);
        registerResult.Should().NotBeNull();
        registerResult!.IsSuccess.Should().BeTrue();
        registerResult.Value.Should().NotBeNull();

        var confirmResponse = await client.PostAsJsonAsync("/api/Account/ConfirmEmail", new ConfirmEmailDto
        {
            EmailConfirmToken = registerResult.Value!.Id.ToString(),
            Token = registerResult.Value.Code
        });
        var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResult>(JsonOptions.CaseInsensitive);
        confirmResult!.IsSuccess.Should().BeTrue();

        var loginResponse = await client.PostAsJsonAsync("/api/Account/Login", new AccountLoginDto { Login = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResult<AccountDto>>(JsonOptions.CaseInsensitive);
        loginResult!.IsSuccess.Should().BeTrue();
        loginResult.Value!.EmailConfirmed.Should().BeTrue();
        loginResult.Value.AccessToken.Should().BeEmpty();

        var userDataResponse = await client.GetAsync("/api/Account/GetUserData");
        var userDataResult = await userDataResponse.Content.ReadFromJsonAsync<ApiResult<AccountDto>>(JsonOptions.CaseInsensitive);
        userDataResult!.IsSuccess.Should().BeTrue();
        userDataResult.Value!.Email.Should().Be(email);

        var refreshResponse = await client.PostAsJsonAsync<RefreshTokenRequest?>("/api/Account/Refresh", null);
        var refreshResult = await refreshResponse.Content.ReadFromJsonAsync<ApiResult<RefreshTokenResponse>>(JsonOptions.CaseInsensitive);
        refreshResult!.IsSuccess.Should().BeTrue();

        var logoutResponse = await client.PostAsJsonAsync<RefreshTokenRequest?>("/api/Account/Logout", null);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var postLogoutResponse = await client.GetAsync("/api/Account/GetUserData");
        postLogoutResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Fails()
    {
        var client = fixture.Factory.CreateClient();
        var email = $"integration-{Guid.NewGuid():N}@example.com";
        var payload = new AccountLoginDto { Login = email, Password = "IntegrationTest@123" };

        (await client.PostAsJsonAsync("/api/Account/Register", payload)).EnsureSuccessStatusCode();
        var secondResponse = await client.PostAsJsonAsync("/api/Account/Register", payload);

        var result = await secondResponse.Content.ReadFromJsonAsync<ApiResult<EmailConfirmationCodes>>(JsonOptions.CaseInsensitive);
        result!.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserData_WithoutAuth_ReturnsUnauthorized()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/Account/GetUserData");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
