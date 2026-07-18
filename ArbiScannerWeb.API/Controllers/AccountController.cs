using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Extensions;
using ArbiScannerWeb.Infrastructure.Settings;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ArbiScannerWeb.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/{controller}")]
    public class AccountController : ControllerBase
    {
        private const string AccessTokenCookieName = "arbiscanner.access_token";
        private const string RefreshTokenCookieName = "arbiscanner.refresh_token";

        private readonly IAccountService _accountService;
        private readonly IUserSettingsService _telegramUserService;
        private readonly JwtOptions _jwtOptions;

        public AccountController(IAccountService accountService, IUserSettingsService telegramUserService, IOptions<JwtOptions> jwtOptions)
        {
            _accountService = accountService;
            _telegramUserService = telegramUserService;
            _jwtOptions = jwtOptions.Value;
        }

        [AllowAnonymous]
        [HttpPost("Login")]
        public async Task<ActionResult<Result<AccountDto>>> Login([FromBody] AccountLoginDTO model)
        {
            var result = await _accountService.Login(model);
            if (result.IsSuccess && result.Value.EmailConfirmed)
            {
                AppendAuthCookies(result.Value.AccessToken, result.Value.RefreshToken);
                ClearAuthTokens(result.Value);
            }

            return result.ToSerializable();
        }

        [AllowAnonymous]
        [HttpPost("Register")]
        public async Task<ActionResult<Result<EmailConfirmationCodes>>> RegisterUser([FromBody] AccountLoginDTO model)
        {
            return (await _accountService.RegisterUser(model)).ToSerializable();
        }

        [AllowAnonymous]
        [HttpPost("ForgotPassword")]
        public async Task<ActionResult<Result>> SendForgetPasswordCode([FromBody] ForgotPasswordDTO forgotPasswordDTO)
        {
            return (await _accountService.SendForgetPasswordCode(forgotPasswordDTO.Email)).ToSerializable();
        }

        [AllowAnonymous]
        [HttpPost("ResetPassword")]
        public async Task<ActionResult<Result>> ResetPassword([FromBody] ChangePasswordDTO changePasswordDTO)
        {
            return (await _accountService.ResetPassword(changePasswordDTO)).ToSerializable();
        }
        [Authorize]
        [HttpPost("UpdateDetails")]
        public async Task<ActionResult<Result<AccountEditDTO>>> UpdateDetails([FromBody] AccountEditDTO account)
        {
            return (await _accountService.UpdateDetails(account)).ToSerializable();
        }

        [AllowAnonymous]
        [HttpPost("ConfirmEmail")]
        public async Task<ActionResult<Result>> ConfirmEmail([FromBody] ConfirmEmailDTO emailConfirmationDTO)
        {
            return (await _accountService.CheckConfirmationCodeEmail(emailConfirmationDTO.EmailConfirmToken,emailConfirmationDTO.Token)).ToSerializable();
        }
        [HttpPost("ChangeEmailRequest")]
        public async Task<ActionResult<Result<EmailConfirmationCodes>>> ChangeEmailRequest([FromBody] ChangeEmailRequestDTO changeEmailRequestDTO)
        {
            return (await _accountService.ChangeEmailRequest(changeEmailRequestDTO.Email)).ToSerializable();
        }
        [AllowAnonymous]
        [HttpPost("ResendEmailConfirmation")]
        public async Task<ActionResult<Result>> ResendEmailConfirmation([FromBody] ConfirmEmailDTO emailConfirmationDTO)

        {
            return (await _accountService.ResendEmailConfirmation(emailConfirmationDTO.EmailConfirmToken)).ToSerializable();
        }
        [Authorize]
        [HttpGet("GetUserData")]
        public async Task<ActionResult<Result<AccountDto>>> GetUserData()
        {
            return (await _accountService.GetUserData()).ToSerializable();
        }

        [Authorize]
        [HttpPost("CreateTelegramLinkRequest")]
        public async Task<ActionResult<Result<TelegramLinkRequest>>> CreateTelegramLinkRequest()
        {
            return (await _telegramUserService.CreateLinkRequestAsyncForAuthUser()).ToSerializable();
        }

        [Authorize]
        [HttpPost("RemoveTelegramLink")]
        public async Task<ActionResult<Result>> RemoveTelegramLink()
        {
            return (await _telegramUserService.RemoveTelegramLinkAsyncForAuthUser()).ToSerializable();
        }

        [AllowAnonymous]
        [HttpPost("Refresh")]
        public async Task<ActionResult<Result<RefreshTokenResponse>>> Refresh([FromBody] RefreshTokenRequest? request)
        {
            var refreshToken = ResolveRefreshToken(request);
            if (string.IsNullOrEmpty(refreshToken))
            {
                return BadRequest(Result.Fail(TypedErrors.Validation("Refresh token is required.")).ToResult<RefreshTokenResponse>().ToSerializable());
            }

            var result = await _accountService.RefreshAccessToken(refreshToken);
            if (result.IsSuccess)
            {
                AppendAuthCookies(result.Value.AccessToken, result.Value.RefreshToken);
                result.Value.AccessToken = string.Empty;
                result.Value.RefreshToken = string.Empty;
            }

            return result.ToSerializable();
        }

        [Authorize]
        [HttpPost("Logout")]
        public async Task<ActionResult<Result>> Logout([FromBody] RefreshTokenRequest? request)
        {
            var userId = User.Identity?.Name;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(Result.Fail(TypedErrors.Unauthorized("User ID not found in token")).ToSerializable());
            }

            var refreshToken = ResolveRefreshToken(request);
            if (string.IsNullOrEmpty(refreshToken))
            {
                ClearAuthCookies();
                return Result.Ok().ToSerializable();
            }

            var result = await _accountService.LogoutByToken(userId, refreshToken);
            ClearAuthCookies();
            return result.ToSerializable();
        }

        private string? ResolveRefreshToken(RefreshTokenRequest? request)
        {
            if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
            {
                return request.RefreshToken;
            }

            return Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken)
                ? refreshToken
                : null;
        }

        private void AppendAuthCookies(string accessToken, string refreshToken)
        {
            Response.Cookies.Append(
                AccessTokenCookieName,
                accessToken,
                CreateCookieOptions(TimeSpan.FromMinutes(_jwtOptions.AccessTokenExpirationMinutes))
            );

            Response.Cookies.Append(
                RefreshTokenCookieName,
                refreshToken,
                CreateCookieOptions(TimeSpan.FromDays(_jwtOptions.RefreshTokenExpirationDays))
            );
        }

        private CookieOptions CreateCookieOptions(TimeSpan lifetime)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.Add(lifetime),
                Path = "/"
            };
        }

        private void ClearAuthCookies()
        {
            Response.Cookies.Delete(AccessTokenCookieName, new CookieOptions { Path = "/" });
            Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/" });
        }

        private static void ClearAuthTokens(AccountDto accountDto)
        {
            accountDto.Token = string.Empty;
            accountDto.AccessToken = string.Empty;
            accountDto.RefreshToken = string.Empty;
        }
    }
}