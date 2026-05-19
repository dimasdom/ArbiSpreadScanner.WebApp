namespace ArbiScannerWeb.Domain.Models
{
    /// <summary>
    /// Represents a refresh token issued to a user for obtaining new access tokens.
    /// Supports token rotation, revocation detection, and session tracking.
    /// </summary>
    public class RefreshTokenModel
    {
        /// <summary>
        /// Primary key - unique identifier for this refresh token record.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Foreign key - the user who owns this refresh token.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Navigation property to the associated user account.
        /// </summary>
        public virtual AccountModel? User { get; set; }

        /// <summary>
        /// SHA256 hash of the actual token (stored for security, not the token itself).
        /// </summary>
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>
        /// The date and time when this token expires (valid until this moment).
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// The date and time when this token was created/issued.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The date and time when this token was revoked (if applicable).
        /// Null if the token has not been revoked.
        /// </summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>
        /// The reason this token was revoked (e.g., "logout", "suspicious_reuse", "token_reissued").
        /// </summary>
        public string? RevocationReason { get; set; }

        /// <summary>
        /// The ID of the refresh token that replaced this one during token rotation.
        /// Used to detect suspicious reuse of old tokens.
        /// </summary>
        public Guid? ReplacedByTokenId { get; set; }

        /// <summary>
        /// Navigation property to the replacement token (if this token was rotated).
        /// </summary>
        public virtual RefreshTokenModel? ReplacedByToken { get; set; }

        /// <summary>
        /// The IP address from which this token was created.
        /// Helps identify suspicious token usage patterns.
        /// </summary>
        public string? CreatedByIp { get; set; }

        /// <summary>
        /// The IP address from which this token was revoked (if applicable).
        /// </summary>
        public string? RevokedByIp { get; set; }

        /// <summary>
        /// The user agent (browser/client info) from which this token was created.
        /// Optional: helps with device tracking and session management UI.
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// Optional device identifier (e.g., UUID) for multi-device session management.
        /// Can be used to show "devices you're logged in on" UI.
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Determines if this token is currently valid (not expired and not revoked).
        /// </summary>
        public bool IsActive => !IsExpired && !IsRevoked;

        /// <summary>
        /// Determines if this token has expired based on current UTC time.
        /// </summary>
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        /// <summary>
        /// Determines if this token has been revoked.
        /// </summary>
        public bool IsRevoked => RevokedAt.HasValue;
    }
}
