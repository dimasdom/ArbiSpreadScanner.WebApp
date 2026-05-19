using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Domain.Models.DTOs
{
    public class AccountDto
    {
        public AccountDto() { }

        public string Id { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public UserSettingsModel UserSettings { get; set; } = new();

        /// <summary>
        /// The access token (JWT) for authenticating API requests.
        /// Legacy field name - new code should use AccessToken.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// The access token (JWT) for authenticating API requests.
        /// Short-lived token (typically 15 minutes).
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// The refresh token for obtaining new access tokens.
        /// Long-lived token (typically 7 days), used only with the refresh endpoint.
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// Token used for email confirmation flow.
        /// </summary>
        public string EmailConfirmToken { get; set; } = string.Empty;
    }
}
