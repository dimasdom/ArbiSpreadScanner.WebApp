using ArbiScannerWeb.Domain.Models;
using FluentResults;

namespace ArbiScannerWeb.Abstractions.Interfaces;
public interface IUserSettingsService
{
    Task<Result<TelegramLinkRequest>> CreateLinkRequestAsyncForAuthUser();
    Task<Result> RemoveTelegramLinkAsyncForAuthUser();
    Task<Result<TelegramLinkRequest>> CreateLinkRequestAsync(string accountId);
    Task<Result> RemoveTelegramLinkAsync(string accountId);
    Task<Result<UserSettingsModel?>> GetUserSettingsByAccountIdAsync(string accountId);
}