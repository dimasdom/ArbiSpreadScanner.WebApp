using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.API.Controllers;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Settings;
using ArbiScannerWeb.Tests.Helpers;
using FluentAssertions;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace ArbiScannerWeb.Tests.API;

public class AccountControllerTests
{
    private readonly Mock<IAccountService> _accountService = new();
    private readonly Mock<IUserSettingsService> _userSettings = new();

    private AccountController CreateController(DefaultHttpContext? httpContext = null)
    {
        var ctrl = new AccountController(
            _accountService.Object,
            _userSettings.Object,
            Options.Create(MockHelpers.CreateTestJwtOptions()));

        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext ?? new DefaultHttpContext()
        };
        return ctrl;
    }

    [Fact]
    public async Task Login_SuccessWithConfirmedEmail_SetsAuthCookies()
    {
        _accountService.Setup(s => s.Login(It.IsAny<AccountLoginDTO>()))
            .ReturnsAsync(Result.Ok(new AccountDto
            {
                EmailConfirmed = true,
                AccessToken = "access-tok",
                RefreshToken = "refresh-tok"
            }));

        var httpContext = new DefaultHttpContext();
        var ctrl = CreateController(httpContext);

        await ctrl.Login(new AccountLoginDTO { Login = "a@b.com", Password = "pass" });

        var setCookie = string.Join("|", httpContext.Response.Headers["Set-Cookie"].ToArray());
        setCookie.Should().Contain("arbiscanner.access_token");
        setCookie.Should().Contain("arbiscanner.refresh_token");
    }

    [Fact]
    public async Task Login_SuccessButEmailUnconfirmed_DoesNotSetAuthCookies()
    {
        _accountService.Setup(s => s.Login(It.IsAny<AccountLoginDTO>()))
            .ReturnsAsync(Result.Ok(new AccountDto
            {
                EmailConfirmed = false,
                EmailConfirmToken = "token-id"
            }));

        var httpContext = new DefaultHttpContext();
        var ctrl = CreateController(httpContext);

        await ctrl.Login(new AccountLoginDTO { Login = "a@b.com", Password = "pass" });

        httpContext.Response.Headers.ContainsKey("Set-Cookie").Should().BeFalse();
    }

    [Fact]
    public async Task Login_ServiceFails_DoesNotSetCookies()
    {
        _accountService.Setup(s => s.Login(It.IsAny<AccountLoginDTO>()))
            .ReturnsAsync(Result.Fail<AccountDto>("invalid credentials"));

        var httpContext = new DefaultHttpContext();
        var ctrl = CreateController(httpContext);

        await ctrl.Login(new AccountLoginDTO { Login = "x@y.com", Password = "wrong" });

        httpContext.Response.Headers.ContainsKey("Set-Cookie").Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_NullRequestAndNoCookie_ReturnsBadRequest()
    {
        var ctrl = CreateController();

        var result = await ctrl.Refresh(request: null);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Refresh_EmptyRefreshToken_ReturnsBadRequest()
    {
        var ctrl = CreateController();

        var result = await ctrl.Refresh(new RefreshTokenRequest { RefreshToken = "" });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Refresh_ValidToken_ClearsTokensFromResponseBody()
    {
        _accountService.Setup(s => s.RefreshAccessToken(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(Result.Ok(new RefreshTokenResponse
            {
                AccessToken = "new-access",
                RefreshToken = "new-refresh",
                ExpiresIn = 900
            }));

        var httpContext = new DefaultHttpContext();
        var ctrl = CreateController(httpContext);

        var result = await ctrl.Refresh(new RefreshTokenRequest { RefreshToken = "valid-token" });

        var setCookie = string.Join("|", httpContext.Response.Headers["Set-Cookie"].ToArray());
        setCookie.Should().Contain("arbiscanner.access_token");
    }

    [Fact]
    public async Task Logout_NoRefreshToken_ClearsAuthCookiesAndReturnsOk()
    {
        var identity = new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimsIdentity.DefaultNameClaimType, "user-1") },
            "test");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };

        var ctrl = CreateController(httpContext);

        var result = await ctrl.Logout(request: null);

        var setCookie = string.Join("|", httpContext.Response.Headers["Set-Cookie"].ToArray());
        setCookie.Should().Contain("arbiscanner.access_token");
        setCookie.Should().Contain("arbiscanner.refresh_token");
    }
}
