using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.DbContext;
using ArbiScannerWeb.Infrastructure.Repositories;
using ArbiScannerWeb.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class AccountRepositoryTests
{
    private readonly AppDbContext _dbContext = MockHelpers.CreateInMemoryDbContext();
    private readonly AccountRepository _sut;

    public AccountRepositoryTests()
    {
        _sut = new AccountRepository(_dbContext);
    }

    [Fact]
    public async Task GetEmailConfirmationByUserIdAsync_Found_ReturnsCode()
    {
        var code = new EmailConfirmationCodes { Id = Guid.NewGuid(), UserId = "u1", Code = "123456" };
        _dbContext.EmailConfirmationCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetEmailConfirmationByUserIdAsync("u1");

        result!.Code.Should().Be("123456");
    }

    [Fact]
    public async Task GetEmailConfirmationByUserIdAsync_NotFound_ReturnsNull()
    {
        var result = await _sut.GetEmailConfirmationByUserIdAsync("missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEmailConfirmationByIdAsync_Found_ReturnsCode()
    {
        var id = Guid.NewGuid();
        _dbContext.EmailConfirmationCodes.Add(new EmailConfirmationCodes { Id = id, UserId = "u1" });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetEmailConfirmationByIdAsync(id);

        result!.Id.Should().Be(id);
    }

    [Fact]
    public async Task ReplaceEmailConfirmationCodeAsync_RemovesExistingAndAddsNew()
    {
        _dbContext.EmailConfirmationCodes.Add(new EmailConfirmationCodes { Id = Guid.NewGuid(), UserId = "u1", Code = "old" });
        await _dbContext.SaveChangesAsync();
        var newCode = new EmailConfirmationCodes { Id = Guid.NewGuid(), UserId = "u1", Code = "new" };

        await _sut.ReplaceEmailConfirmationCodeAsync("u1", newCode);

        var remaining = _dbContext.EmailConfirmationCodes.Where(c => c.UserId == "u1").ToList();
        remaining.Should().ContainSingle(c => c.Code == "new");
    }

    [Fact]
    public async Task RemoveEmailConfirmationCodeAsync_RemovesCode()
    {
        var code = new EmailConfirmationCodes { Id = Guid.NewGuid(), UserId = "u1" };
        _dbContext.EmailConfirmationCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        await _sut.RemoveEmailConfirmationCodeAsync(code);

        _dbContext.EmailConfirmationCodes.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateEmailConfirmationCodeAsync_PersistsChanges()
    {
        var code = new EmailConfirmationCodes { Id = Guid.NewGuid(), UserId = "u1", Code = "old" };
        _dbContext.EmailConfirmationCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        code.Code = "updated";
        await _sut.UpdateEmailConfirmationCodeAsync(code);

        var reloaded = await _dbContext.EmailConfirmationCodes.FindAsync(code.Id);
        reloaded!.Code.Should().Be("updated");
    }

    [Fact]
    public async Task GetForgetPasswordRequestByIdAsync_Found_ReturnsRequest()
    {
        var id = Guid.NewGuid();
        _dbContext.ForgetPasswordRequests.Add(new ForgetPasswordRequest { Id = id, Token = "tok", UserId = "u1" });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetForgetPasswordRequestByIdAsync(id);

        result!.Token.Should().Be("tok");
    }

    [Fact]
    public async Task ReplaceForgetPasswordRequestAsync_RemovesExistingAndAddsNew()
    {
        _dbContext.ForgetPasswordRequests.Add(new ForgetPasswordRequest { Id = Guid.NewGuid(), Token = "old", UserId = "u1" });
        await _dbContext.SaveChangesAsync();
        var newRequest = new ForgetPasswordRequest { Id = Guid.NewGuid(), Token = "new", UserId = "u1" };

        await _sut.ReplaceForgetPasswordRequestAsync("u1", newRequest);

        var remaining = _dbContext.ForgetPasswordRequests.Where(r => r.UserId == "u1").ToList();
        remaining.Should().ContainSingle(r => r.Token == "new");
    }

    [Fact]
    public async Task AddRefreshTokenAsync_WithoutDetach_KeepsEntityTracked()
    {
        var token = new RefreshTokenModel { UserId = "u1", TokenHash = "hash1", ExpiresAt = DateTime.UtcNow.AddDays(1) };

        await _sut.AddRefreshTokenAsync(token);

        _dbContext.Entry(token).State.Should().Be(EntityState.Unchanged);
    }

    [Fact]
    public async Task AddRefreshTokenAsync_WithDetach_DetachesEntity()
    {
        var token = new RefreshTokenModel { UserId = "u1", TokenHash = "hash2", ExpiresAt = DateTime.UtcNow.AddDays(1) };

        await _sut.AddRefreshTokenAsync(token, detachAfterSave: true);

        _dbContext.Entry(token).State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public async Task GetRefreshTokenByUserAndHashAsync_Found_ReturnsToken()
    {
        var token = new RefreshTokenModel { UserId = "u1", TokenHash = "hash3", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetRefreshTokenByUserAndHashAsync("u1", "hash3");

        result!.TokenHash.Should().Be("hash3");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetRefreshTokenByHashAsync_Found_ReturnsToken(bool forUpdate)
    {
        var token = new RefreshTokenModel { UserId = "u1", TokenHash = "hash4", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetRefreshTokenByHashAsync("hash4", forUpdate);

        result!.TokenHash.Should().Be("hash4");
    }

    [Fact]
    public async Task GetRefreshTokenByIdAsync_Found_ReturnsToken()
    {
        var token = new RefreshTokenModel { UserId = "u1", TokenHash = "hash5", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetRefreshTokenByIdAsync(token.Id);

        result!.Id.Should().Be(token.Id);
    }

    [Fact]
    public async Task GetRefreshTokensByReplacedByTokenIdAsync_ReturnsMatchingTokens()
    {
        var replacementId = Guid.NewGuid();
        _dbContext.RefreshTokens.Add(new RefreshTokenModel
        {
            UserId = "u1", TokenHash = "hash6", ExpiresAt = DateTime.UtcNow.AddDays(1), ReplacedByTokenId = replacementId
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetRefreshTokensByReplacedByTokenIdAsync(replacementId);

        result.Should().ContainSingle(t => t.TokenHash == "hash6");
    }

    [Fact]
    public async Task GetActiveRefreshTokensForUserAsync_ReturnsOnlyActiveTokens()
    {
        var now = DateTime.UtcNow;
        _dbContext.RefreshTokens.AddRange(
            new RefreshTokenModel { UserId = "u1", TokenHash = "active", ExpiresAt = now.AddDays(1) },
            new RefreshTokenModel { UserId = "u1", TokenHash = "expired", ExpiresAt = now.AddDays(-1) },
            new RefreshTokenModel { UserId = "u1", TokenHash = "revoked", ExpiresAt = now.AddDays(1), RevokedAt = now.AddMinutes(-1) });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetActiveRefreshTokensForUserAsync("u1", now);

        result.Should().ContainSingle(t => t.TokenHash == "active");
    }

    [Fact]
    public async Task UpdateRefreshTokenAsync_PersistsChanges()
    {
        var token = new RefreshTokenModel { UserId = "u1", TokenHash = "hash7", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        token.RevokedAt = DateTime.UtcNow;
        await _sut.UpdateRefreshTokenAsync(token);

        var reloaded = await _dbContext.RefreshTokens.FindAsync(token.Id);
        reloaded!.RevokedAt.Should().NotBeNull();
    }
}
