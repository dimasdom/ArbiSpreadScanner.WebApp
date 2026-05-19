namespace ArbiScannerWeb.Infrastructure.Settings
{
    /// <summary>
    /// JWT configuration options for token generation and validation.
    /// </summary>
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        /// <summary>
        /// The signing key used for JWT token generation and validation.
        /// Must be at least 32 characters for HS256.
        /// </summary>
        public string SigningKey { get; set; } = string.Empty;

        /// <summary>
        /// The issuer of the JWT token.
        /// </summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>
        /// The audience for the JWT token.
        /// </summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Access token lifetime in minutes.
        /// </summary>
        public int AccessTokenExpirationMinutes { get; set; } = 15;

        /// <summary>
        /// Refresh token lifetime in days.
        /// </summary>
        public int RefreshTokenExpirationDays { get; set; } = 7;
    }
}
