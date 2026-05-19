namespace ArbiScannerWeb.Domain.Models.DTOs
{
    /// <summary>
    /// DTO for token refresh response.
    /// Contains both the new access token and rotated refresh token.
    /// </summary>
    public class RefreshTokenResponse
    {
        /// <summary>
        /// The new access token (short-lived, typically 15 minutes).
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// The rotated refresh token (long-lived, typically 7 days).
        /// This is a new token that replaced the old one.
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// The expiration time of the new access token (Unix timestamp).
        /// </summary>
        public long ExpiresIn { get; set; }
    }
}
