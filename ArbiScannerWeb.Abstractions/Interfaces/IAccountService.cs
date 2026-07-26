using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Abstractions.Interfaces
{
    public interface IAccountService
    {
        public Task<Result<AccountDto>> Login(AccountLoginDto model);
        public Task<Result<EmailConfirmationCodes>> RegisterUser(AccountLoginDto model);
        public Task<Result<EmailConfirmationCodes>> SendConfirmationEmail(string email, AccountModel account);
        public Task<Result> CheckConfirmationCodeEmail(string emailConfirmId, string code);
        public Task<Result> SendForgetPasswordCode(string email);
        public Task<Result> ResetPassword(ChangePasswordDto changePasswordDTO);
        public Task<Result<AccountEditDto>> UpdateDetails(AccountEditDto account);
        public Task<Result<EmailConfirmationCodes>> ChangeEmailRequest(string newEmail);
        public Task<Result> ResendEmailConfirmation(string EmailConfirmToken);
        public Task<Result<AccountDto>> GetUserData();
        
        /// <summary>
        /// Refreshes an expired or nearly-expired access token using a valid refresh token.
        /// Rotates the refresh token and returns both new access and refresh tokens.
        /// </summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <param name="ipAddress">The IP address of the request.</param>
        /// <param name="userAgent">The user agent of the request.</param>
        /// <returns>RefreshTokenResponse with new access and refresh tokens, or an error result.</returns>
        public Task<Result<RefreshTokenResponse>> RefreshAccessToken(string refreshToken, string? ipAddress = null, string? userAgent = null);

        /// <summary>
        /// Internal refresh method that uses known user id.
        /// </summary>
        public Task<Result<RefreshTokenResponse>> RefreshAccessToken(string userId, string refreshToken, string? ipAddress = null, string? userAgent = null);

        /// <summary>
        /// Logs out the user by revoking their refresh token.
        /// </summary>
        /// <param name="tokenId">The ID of the refresh token to revoke.</param>
        /// <param name="ipAddress">The IP address of the request.</param>
        /// <returns>Success result or error.</returns>
        public Task<Result> Logout(Guid tokenId, string? ipAddress = null);

        /// <summary>
        /// Logs out the user by revoking a refresh token using its value.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="refreshToken">The refresh token value to revoke.</param>
        /// <param name="ipAddress">The IP address of the request.</param>
        /// <returns>Success result or error.</returns>
        public Task<Result> LogoutByToken(string userId, string refreshToken, string? ipAddress = null);
    }
}
