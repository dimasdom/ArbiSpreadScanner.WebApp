using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.DbContext;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Infrastructure.Services
{
    public class AccountService : IAccountService
    {
        private sealed class CachedAccountData
        {
            public UserSettingsModel UserSettings { get; set; } = new();
            public string? Email { get; set; }
            public bool EmailConfirmed { get; set; }
        }

        private const string UserNotFoundMessage = "User not found.";

        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IDatabase _redis;

        public AccountService(AppDbContext context, IHttpContextAccessor httpContextAccessor, IConnectionMultiplexer redis)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _redis = redis.GetDatabase();
        }

        public async Task<Result<AccountEditDto>> UpdateDetails(AccountEditDto account)
        {
            var userIdString = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrEmpty(userIdString))
            {
                return Result.Fail(TypedErrors.Unauthorized("User is not authenticated."));
            }
            var accountModel = await _context.Users
                .Include(a => a.UserSettings)
                    .ThenInclude(s => s.Exchanges)
                .FirstOrDefaultAsync(a => a.Id == userIdString);
            if (accountModel is null)
                return Result.Fail(TypedErrors.NotFound("There are no user"));
            MapAccountEditDTOToAccountModel(account, accountModel);
            var exchangeNames = account.Exchanges
                .Select(e => e.Exchange?.Name)
                .Where(n => n != null)
                .ToList();
            var exchangeModels = await _context.Exchanges
                .Where(e => exchangeNames.Contains(e.Name))
                .ToListAsync();

            _context.UserExchanges.RemoveRange(accountModel.UserSettings.Exchanges);
            accountModel.UserSettings.Exchanges = exchangeModels
                .Select(e => new UserExchangeModel { ExchangeId = e.Id, UserAccountId = accountModel.Id, Exchange = e })
                .ToList();
            _context.Users.Update(accountModel);
            await _context.SaveChangesAsync();
            await _redis.StringSetAsync($"userEntity:{accountModel.Id}", SerializeCachedAccount(accountModel), System.TimeSpan.FromHours(1));
            account.Exchanges = accountModel.UserSettings.Exchanges;
            return Result.Ok(account);
        }

        private static void MapAccountEditDTOToAccountModel(AccountEditDto dto, AccountModel model)
        {
            model.UserSettings.SpreadSize = dto.SpreadSize;
            model.UserSettings.PositionSize = dto.PositionSize;
            model.UserSettings.FuturesSpread = dto.FuturesSpread;
            model.UserSettings.FundingSpread = dto.FundingSpread;
            model.UserSettings.SpotSpread = dto.SpotSpread;
        }

        public async Task<Result<AccountDto>> GetUserData()
        {
            var userId = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            var userEntity = await _redis.StringGetAsync($"userEntity:{userId}");
            if (!userEntity.IsNullOrEmpty)
            {
                var cachedUser = Newtonsoft.Json.JsonConvert.DeserializeObject<CachedAccountData>(userEntity!);
                if (cachedUser != null)
                {
                    var accountredisDto = new AccountDto();
                    accountredisDto.UserSettings = cachedUser.UserSettings;
                    accountredisDto.Email = cachedUser.Email;
                    accountredisDto.EmailConfirmed = cachedUser.EmailConfirmed;
                    return Result.Ok(accountredisDto);
                }
            }
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Fail(TypedErrors.Unauthorized("User is not authenticated."));
            }
            var user = await _context.Users.Include(u => u.UserSettings).ThenInclude(u => u.Exchanges).ThenInclude(e => e.Exchange).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Result.Fail(TypedErrors.NotFound(UserNotFoundMessage));
            }
            var accountDto = new AccountDto();
            accountDto.UserSettings = user.UserSettings;
            accountDto.Email = user.Email;
            accountDto.EmailConfirmed = user.EmailConfirmed;
            await _redis.StringSetAsync($"userEntity:{user.Id}", SerializeCachedAccount(user), System.TimeSpan.FromHours(1));
            return Result.Ok(accountDto);
        }

        private string SerializeCachedAccount(AccountModel account)
        {
            var cached = new CachedAccountData
            {
                UserSettings = CloneTelegramUser(account.UserSettings),
                Email = account.Email,
                EmailConfirmed = account.EmailConfirmed
            };

            return Newtonsoft.Json.JsonConvert.SerializeObject(cached);
        }

        private UserSettingsModel CloneTelegramUser(UserSettingsModel source)
        {
            if (source == null)
            {
                return new UserSettingsModel();
            }

            return new UserSettingsModel
            {
                Id = source.Id,
                AccountId = source.AccountId,
                UserName = source.UserName,
                ChatId = source.ChatId,
                SpreadSize = source.SpreadSize,
                PositionSize = source.PositionSize,
                FuturesSpread = source.FuturesSpread,
                FundingSpread = source.FundingSpread,
                SpotSpread = source.SpotSpread,
                HaveAccess = source.HaveAccess,
                Active = source.Active,
                Exchanges = source.Exchanges?.ToList() ?? new List<UserExchangeModel>()
            };
        }
    }
}
