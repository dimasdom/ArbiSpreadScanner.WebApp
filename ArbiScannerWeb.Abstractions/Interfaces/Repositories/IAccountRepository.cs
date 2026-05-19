using ArbiScannerWeb.Domain.Models;

namespace ArbiScannerWeb.Abstractions.Interfaces.Repositories;

public interface IAccountRepository
{
    Task<EmailConfirmationCodes?> GetEmailConfirmationByUserIdAsync(string userId);
    Task<EmailConfirmationCodes?> GetEmailConfirmationByIdAsync(Guid id);
    Task ReplaceEmailConfirmationCodeAsync(string userId, EmailConfirmationCodes emailCode);
    Task RemoveEmailConfirmationCodeAsync(EmailConfirmationCodes emailCode);
    Task UpdateEmailConfirmationCodeAsync(EmailConfirmationCodes emailCode);

    Task<ForgetPasswordRequest?> GetForgetPasswordRequestByIdAsync(Guid id);
    Task ReplaceForgetPasswordRequestAsync(string userId, ForgetPasswordRequest request);

    Task AddRefreshTokenAsync(RefreshTokenModel refreshToken, bool detachAfterSave = false);
    Task<RefreshTokenModel?> GetRefreshTokenByUserAndHashAsync(string userId, string tokenHash);
    Task<RefreshTokenModel?> GetRefreshTokenByHashAsync(string tokenHash, bool asNoTracking = false);
    Task<RefreshTokenModel?> GetRefreshTokenByIdAsync(Guid tokenId);
    Task<List<RefreshTokenModel>> GetRefreshTokensByReplacedByTokenIdAsync(Guid replacedByTokenId);
    Task<List<RefreshTokenModel>> GetActiveRefreshTokensForUserAsync(string userId, DateTime utcNow);
    Task UpdateRefreshTokenAsync(RefreshTokenModel refreshToken);
}
