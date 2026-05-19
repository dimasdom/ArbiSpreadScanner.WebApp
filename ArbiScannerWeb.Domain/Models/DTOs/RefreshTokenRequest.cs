namespace ArbiScannerWeb.Domain.Models.DTOs
{
    /// <summary>
    /// DTO for token refresh request.
    /// </summary>
    public class RefreshTokenRequest
    {
        /// <summary>
        /// The refresh token to use for obtaining a new access token.
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;
    }
}
