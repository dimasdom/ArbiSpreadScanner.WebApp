using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace ArbiScannerWeb.Infrastructure.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _context;

    public AccountRepository(AppDbContext context)
    {
        _context = context;
    }

    private IQueryable<RefreshTokenModel> RefreshTokensQuery(bool forUpdate = false) =>
        forUpdate
            ? _context.RefreshTokens.AsTracking()
            : _context.RefreshTokens.AsNoTracking();

    public Task<EmailConfirmationCodes?> GetEmailConfirmationByUserIdAsync(string userId)
        => _context.EmailConfirmationCodes.AsNoTracking().FirstOrDefaultAsync(ec => ec.UserId == userId);

    public Task<EmailConfirmationCodes?> GetEmailConfirmationByIdAsync(Guid id)
        => _context.EmailConfirmationCodes.AsNoTracking().FirstOrDefaultAsync(ec => ec.Id == id);

    public async Task ReplaceEmailConfirmationCodeAsync(string userId, EmailConfirmationCodes emailCode)
    {
        var existingCodes = _context.EmailConfirmationCodes.Where(ec => ec.UserId == userId);
        _context.EmailConfirmationCodes.RemoveRange(existingCodes);
        _context.EmailConfirmationCodes.Add(emailCode);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveEmailConfirmationCodeAsync(EmailConfirmationCodes emailCode)
    {
        _context.EmailConfirmationCodes.Remove(emailCode);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateEmailConfirmationCodeAsync(EmailConfirmationCodes emailCode)
    {
        _context.EmailConfirmationCodes.Update(emailCode);
        await _context.SaveChangesAsync();
    }

    public Task<ForgetPasswordRequest?> GetForgetPasswordRequestByIdAsync(Guid id)
        => _context.ForgetPasswordRequests.AsNoTracking().FirstOrDefaultAsync(fpr => fpr.Id == id);

    public async Task ReplaceForgetPasswordRequestAsync(string userId, ForgetPasswordRequest request)
    {
        var existingRequests = _context.ForgetPasswordRequests.Where(fpr => fpr.UserId == userId);
        _context.ForgetPasswordRequests.RemoveRange(existingRequests);
        _context.ForgetPasswordRequests.Add(request);
        await _context.SaveChangesAsync();
    }

    public async Task AddRefreshTokenAsync(RefreshTokenModel refreshToken, bool detachAfterSave = false)
    {
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();
        if (detachAfterSave)
        {
            _context.Entry(refreshToken).State = EntityState.Detached;
        }
    }

    public Task<RefreshTokenModel?> GetRefreshTokenByUserAndHashAsync(string userId, string tokenHash)
        => RefreshTokensQuery().FirstOrDefaultAsync(rt => rt.UserId == userId && rt.TokenHash == tokenHash);

    public Task<RefreshTokenModel?> GetRefreshTokenByHashAsync(string tokenHash, bool forUpdate = false)
        => RefreshTokensQuery(forUpdate).FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

    public Task<RefreshTokenModel?> GetRefreshTokenByIdAsync(Guid tokenId)
        => RefreshTokensQuery().FirstOrDefaultAsync(rt => rt.Id == tokenId);

    public Task<List<RefreshTokenModel>> GetRefreshTokensByReplacedByTokenIdAsync(Guid replacedByTokenId)
        => RefreshTokensQuery().Where(rt => rt.ReplacedByTokenId == replacedByTokenId).ToListAsync();

    public Task<List<RefreshTokenModel>> GetActiveRefreshTokensForUserAsync(string userId, DateTime utcNow)
        => RefreshTokensQuery()
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > utcNow)
            .ToListAsync();

    public async Task UpdateRefreshTokenAsync(RefreshTokenModel refreshToken)
    {
        _context.RefreshTokens.Update(refreshToken);
        await _context.SaveChangesAsync();
    }
}
