using ArbiScannerWeb.Domain.Models;
using FluentAssertions;

namespace ArbiScannerWeb.Tests.Domain;

public class RefreshTokenModelTests
{
    [Fact]
    public void IsActive_NotExpiredAndNotRevoked_ReturnsTrue()
    {
        var token = new RefreshTokenModel { ExpiresAt = DateTime.UtcNow.AddDays(1), RevokedAt = null };
        token.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_Expired_ReturnsFalse()
    {
        var token = new RefreshTokenModel { ExpiresAt = DateTime.UtcNow.AddDays(-1) };
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_Revoked_ReturnsFalse()
    {
        var token = new RefreshTokenModel
        {
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = DateTime.UtcNow
        };
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_ExpiresAtInPast_ReturnsTrue()
    {
        var token = new RefreshTokenModel { ExpiresAt = DateTime.UtcNow.AddSeconds(-1) };
        token.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_ExpiresAtInFuture_ReturnsFalse()
    {
        var token = new RefreshTokenModel { ExpiresAt = DateTime.UtcNow.AddDays(1) };
        token.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsRevoked_RevokedAtHasValue_ReturnsTrue()
    {
        var token = new RefreshTokenModel { RevokedAt = DateTime.UtcNow };
        token.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void IsRevoked_RevokedAtIsNull_ReturnsFalse()
    {
        var token = new RefreshTokenModel { RevokedAt = null };
        token.IsRevoked.Should().BeFalse();
    }
}
