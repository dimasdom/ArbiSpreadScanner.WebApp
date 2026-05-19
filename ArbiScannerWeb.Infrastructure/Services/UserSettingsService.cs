using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.DbContext;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ArbiScannerWeb.Infrastructure.Services;
public class UserSettingsService : IUserSettingsService
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDatabase _redis;

    public UserSettingsService(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor, IConnectionMultiplexer redis)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _redis = redis.GetDatabase();
    }

    public async Task<Result<TelegramLinkRequest>> CreateLinkRequestAsync(string accountId)
    {
        var account = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == accountId);
        if (account == null)        {
            return Result.Fail<TelegramLinkRequest>("Account not found");
        }
        var telegramAccount = await _dbContext.UsersSettings.FirstOrDefaultAsync(t => t.Id == account.UserSettingsId);
        if (telegramAccount == null)
        {
            return Result.Fail<TelegramLinkRequest>("Telegram account not found for this user");
        }
        telegramAccount.AccountId = account.Id;
        var linkRequest = new TelegramLinkRequest
        {
            AccountId = account.Id
        };
        _dbContext.TelegramLinkRequests.Add(linkRequest);
        await _dbContext.SaveChangesAsync();
        await _redis.KeyDeleteAsync($"userEntity:{account.Id}");
        return Result.Ok(new TelegramLinkRequest
        {
            Id = linkRequest.Id,
            AccountId = linkRequest.AccountId
        });
    }

    public Task<Result<TelegramLinkRequest>> CreateLinkRequestAsyncForAuthUser()
    {
            var userIdString = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrEmpty(userIdString))
            {
                return Task.FromResult(Result.Fail<TelegramLinkRequest>("User is not authenticated"));
            }
            return CreateLinkRequestAsync(userIdString);
    }

    public async Task<Result<UserSettingsModel?>> GetUserSettingsByAccountIdAsync(string accountId)
    {
        var account = await _dbContext.Users.FindAsync(accountId);
        if (account == null)
        {
            return Result.Fail<UserSettingsModel?>("Account not found");
        }
        var userSettings = await _dbContext.UsersSettings.FindAsync(account.UserSettingsId);
        return Result.Ok(userSettings);   
    }

    public async Task<Result> RemoveTelegramLinkAsync(string accountId)
    {
        var account = await _dbContext.Users.Include(a=>a.UserSettings).FirstOrDefaultAsync(a => a.Id == accountId);
        if (account == null)
        {
            return Result.Fail("Account not found");
        }
        var telegramLink = _dbContext.TelegramLinkRequests.Where(t => t.AccountId == account.Id);
        _dbContext.TelegramLinkRequests.RemoveRange(telegramLink);
        account.UserSettings.ChatId = 0;
        account.UserSettings.UserName = null;
        await _dbContext.SaveChangesAsync();
        await _redis.KeyDeleteAsync($"userEntity:{account.Id}");
        return Result.Ok();
    }

    public Task<Result> RemoveTelegramLinkAsyncForAuthUser()
    {
        var userIdString = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (string.IsNullOrEmpty(userIdString))
        {
            return Task.FromResult(Result.Fail("User is not authenticated"));
        }
        return RemoveTelegramLinkAsync(userIdString);
    }
}