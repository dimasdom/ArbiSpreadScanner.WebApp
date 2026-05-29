using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.DbContext;
using ArbiScannerWeb.Infrastructure.Settings;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
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

        private readonly IEmailService _emailService;
        private readonly AppDbContext _context;
        private readonly IAccountRepository _accountRepository;
        private readonly SignInManager<AccountModel> _signInManager;
        private readonly UserManager<AccountModel> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly JwtOptions _jwtOptions;
        private readonly string _clientUrl;
        private static string usernameChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private static string passwordChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private readonly IDatabase _redis;
        private readonly ILogger<AccountService> _logger;

        public AccountService(SignInManager<AccountModel> signInManager,
            UserManager<AccountModel> userManager,
            IConnectionMultiplexer redis,
            IHttpContextAccessor httpContextAccessor,
            AppDbContext appDbContext,
            IAccountRepository accountRepository,
            IEmailService emailService,
            IOptions<JwtOptions> jwtOptions,
            IConfiguration configuration,
            ILogger<AccountService> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _redis = redis.GetDatabase();
            _httpContextAccessor = httpContextAccessor;
            _context = appDbContext;
            _accountRepository = accountRepository;
            _emailService = emailService;
            _jwtOptions = jwtOptions.Value;
            _clientUrl = configuration["ClientUrl"] ?? "http://localhost:3001";
            _logger = logger;
        }

        public async Task<Result<AccountDto>> Login(AccountLoginDTO model)
        {
            var User = await _userManager.FindByNameAsync(model.Login);
            if (User != null)
            {
                
                var res = await _signInManager.CheckPasswordSignInAsync(User, model.Password, false);
                if (res.IsNotAllowed)
                {
                    if(!User.EmailConfirmed)
                    {
                        var emailConfirmation = await _accountRepository.GetEmailConfirmationByUserIdAsync(User.Id);
                        return Result.Ok(new AccountDto
                        {
                            EmailConfirmed = false,
                            EmailConfirmToken = emailConfirmation?.Id.ToString() ?? string.Empty
                        });
                    }
                    return Result.Fail("User is not allowed to sign in.");
                }
                if (res.Succeeded)
                {
                    var claims = new List<Claim>
                {
                    new Claim(ClaimsIdentity.DefaultNameClaimType, User.Id.ToString())
                };
                    ClaimsIdentity claimsIdentity =
                    new ClaimsIdentity(claims, "Token", ClaimsIdentity.DefaultNameClaimType,
                        ClaimsIdentity.DefaultRoleClaimType);

                    var now = DateTime.UtcNow;
                    // создаем JWT-токен
                    var jwt = new JwtSecurityToken(
                            issuer: _jwtOptions.Issuer,
                            audience: _jwtOptions.Audience,
                            notBefore: now,
                            claims: claimsIdentity.Claims,
                    expires: now.Add(TimeSpan.FromMinutes(_jwtOptions.AccessTokenExpirationMinutes)),
                            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_jwtOptions.SigningKey)), SecurityAlgorithms.HmacSha256));
                    var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);
                    
                    // Generate refresh token
                    var ipAddress = GetClientIpAddress();
                    var userAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();
                    var refreshTokenModel = await GenerateRefreshToken(User.Id, ipAddress, userAgent);
                    
                    var userWithTelegram = await _context.Users.Include(x => x.UserSettings).ThenInclude(u=>u.Exchanges).ThenInclude(e => e.Exchange).FirstOrDefaultAsync(x => x.Id == User.Id);
                    var accountSource = userWithTelegram ?? User;

                    var accountDto = new AccountDto();
                    accountDto.Id = User.Id;
                    accountDto.UserSettings = accountSource.UserSettings;
                    accountDto.Token = encodedJwt;
                    accountDto.AccessToken = encodedJwt;
                    accountDto.RefreshToken = refreshTokenModel.TokenHash; // TokenHash contains the actual token after GenerateRefreshToken
                    accountDto.Email = User.Email;
                    accountDto.EmailConfirmed = User.EmailConfirmed;
                    await _redis.StringSetAsync($"userEntity:{User.Id}", SerializeCachedAccount(accountSource), TimeSpan.FromHours(1));
                    return Result.Ok(accountDto);
                }
            }
            return Result.Fail("No User");

        }
        /// <summary>
        /// Generates a random username with 8 alphabetic characters
        /// </summary>
        /// <returns>8-character username string</returns>
        static string GenerateUsername()
        {
            Random random = new Random();
            StringBuilder username = new StringBuilder();

            for (int i = 0; i < 8; i++)
            {
                int index = random.Next(usernameChars.Length);
                username.Append(usernameChars[index]);
            }

            return username.ToString();
        }

        /// <summary>
        /// Generates a random password with 8 alphanumeric characters
        /// </summary>
        /// <returns>8-character password string</returns>
        static string GeneratePassword()
        {
            Random random = new Random();
            StringBuilder password = new StringBuilder();

            for (int i = 0; i < 8; i++)
            {
                int index = random.Next(passwordChars.Length);
                password.Append(passwordChars[index]);
            }

            return password.ToString();
        }

        public async Task<Result<EmailConfirmationCodes>> RegisterUser(AccountLoginDTO model)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(model.Login);
                if (user != null)
                {
                    return Result.Fail("User with such email already exists");
                }
                var newUser = new AccountModel();
                newUser.UserName = model.Login;
                newUser.Email = model.Login;
                newUser.UserSettings.AccountId = newUser.Id;
                var res = await _userManager.CreateAsync(newUser, model.Password);            
                if (res.Succeeded)
                {
                    var emailres = await SendConfirmationEmail(model.Login,newUser);
                    if(emailres.IsFailed)
                    {
                    return Result.Fail("Failed to send confirmation email.");
                    }
                    return Result.Ok(emailres.Value);
                }
                else
                {
                    return Result.Fail(string.Join("; ", res.Errors.Select(e => e.Description)));
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Registration failed for {Login}", model.Login);
                return Result.Fail($"Registration failed: {ex.Message}");
            }
        }

        public async Task<Result> CheckConfirmationCodeEmail(string emailConfirmId, string code)
        {
            try
            {
                EmailConfirmationCodes? emailConfirmation = null;
                if(!string.IsNullOrEmpty(emailConfirmId))
                {
                    emailConfirmation = await _accountRepository.GetEmailConfirmationByIdAsync(new Guid(emailConfirmId));
                }
                
                if (emailConfirmation == null)
                {
                    return Result.Fail("Invalid confirmation code.");
                }
                if (emailConfirmation.ExpirationTime < DateTime.UtcNow)
                {
                    return Result.Fail("Confirmation code has expired.");
                }
                if(emailConfirmation.Code != code)
                {
                    return Result.Fail("Invalid confirmation code.");
                }
                var user =  await _userManager.FindByIdAsync(emailConfirmation.UserId);
                if(user == null)
                {
                    return Result.Fail("User not found.");
                }
                user.UserName = emailConfirmation.Email;
                user.Email = emailConfirmation.Email;
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
                await _accountRepository.RemoveEmailConfirmationCodeAsync(emailConfirmation);
                return Result.Ok();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Email confirmation failed for token {EmailConfirmId}", emailConfirmId);
                return Result.Fail($"Email confirmation failed: {ex.Message}");
            }
        }

        private int GenerateConfirmationCode()
        {
            Random random = new Random();
            return random.Next(100000, 999999);
        }

        public async Task<Result> SendForgetPasswordCode(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return Result.Fail("User with such email does not exist.");
            }
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            var forgetPasswordRequest = new ForgetPasswordRequest
            {
                Token = code,
                UserId = user.Id
            };
            await _accountRepository.ReplaceForgetPasswordRequestAsync(user.Id, forgetPasswordRequest);
            var res = await _emailService.SendEmailAsync(email, "Password Reset Code", $"Follow this link to reset your password: <a href=\"{_clientUrl}/account/resetpassword?token={forgetPasswordRequest.Id}\">Reset Password</a>");
            if (res.IsFailed)
            {
                return Result.Fail("Failed to send email.");
            }
            return Result.Ok();
        }

        public async Task<Result> ResetPassword(ChangePasswordDTO changePasswordDTO)
        {
            var ForgetPasswordRequest = await _accountRepository.GetForgetPasswordRequestByIdAsync(Guid.Parse(changePasswordDTO.Token));
            if(ForgetPasswordRequest == null)
            {
                return Result.Fail("Invalid or expired password reset token.");
            }
            var user = await _userManager.FindByIdAsync(ForgetPasswordRequest.UserId);
            if (user == null)
            {
                return Result.Fail("User with such email does not exist.");
            }
            var result = await _userManager.ResetPasswordAsync(user, ForgetPasswordRequest.Token, changePasswordDTO.NewPassword);
            if (result.Succeeded)
            {
                return Result.Ok();
            }
            else
            {
                return Result.Fail(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        public async Task<Result<AccountEditDTO>> UpdateDetails(AccountEditDTO accountDTO)
        {
            var userIdString = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrEmpty(userIdString))
            {
                return Result.Fail("User is not authenticated.");
            }
            var account = await _context.Users
                .Include(a => a.UserSettings)
                    .ThenInclude(s => s.Exchanges)
                .FirstOrDefaultAsync(a => a.Id == userIdString);
            if (account is null)
                return Result.Fail("There are no user");
            MapAccountEditDTOToAccountModel(accountDTO, account);
            var exchangeNames = accountDTO.Exchanges
                .Select(e => e.Exchange?.Name)
                .Where(n => n != null)
                .ToList();
            var exchangeModels = await _context.Exchanges
                .Where(e => exchangeNames.Contains(e.Name))
                .ToListAsync();

            // Remove all existing exchange rows so removals take effect and duplicates cannot form
            _context.UserExchanges.RemoveRange(account.UserSettings.Exchanges);
            account.UserSettings.Exchanges = exchangeModels
                .Select(e => new UserExchangeModel { ExchangeId = e.Id, UserAccountId = account.Id, Exchange = e })
                .ToList();
            _context.Users.Update(account);
            await _context.SaveChangesAsync();
            await _redis.StringSetAsync($"userEntity:{account.Id}", SerializeCachedAccount(account), TimeSpan.FromHours(1));
            accountDTO.Exchanges = account.UserSettings.Exchanges;
            return Result.Ok(accountDTO);
        }

        private void MapAccountEditDTOToAccountModel(AccountEditDTO dto, AccountModel model)
        {
            model.UserSettings.SpreadSize = dto.SpreadSize;
            model.UserSettings.PositionSize = dto.PositionSize;
            model.UserSettings.FuturesSpread = dto.FuturesSpread;
            model.UserSettings.FundingSpread = dto.FundingSpread;
            model.UserSettings.SpotSpread = dto.SpotSpread;
            // Exchanges are mapped separately in UpdateDetails via DB lookup
        }

        public async Task<Result<EmailConfirmationCodes>> SendConfirmationEmail(string email, AccountModel account)
        {
            var code = GenerateConfirmationCode();
            var res = await _emailService.SendEmailAsync(email, "Email Confirmation Code", $"Your confirmation code is: {code}");
            if(res.IsFailed)
            {
               return Result.Fail("Failed to send email.");
            }
            var emailCode = new EmailConfirmationCodes
            {
                UserId = account.Id,
                Email = email,
                Code = code.ToString(),
                ExpirationTime = DateTime.UtcNow.AddMinutes(10)
            };
            await _accountRepository.ReplaceEmailConfirmationCodeAsync(account.Id, emailCode);
            return Result.Ok(emailCode);
        }

        public async Task<Result<EmailConfirmationCodes>> ChangeEmailRequest(string newEmail)
        {
            var userId = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrEmpty(userId))
            {
                return Result.Fail("User is not authenticated.");
            }
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Result.Fail("User not found.");
            }
            var emailRes = await SendConfirmationEmail(newEmail, user);
            if(emailRes.IsFailed)
            {
                return Result.Fail("Failed to send confirmation email.");
            }
            return Result.Ok(emailRes.Value);
        }

        public async Task<Result> ResendEmailConfirmation(string EmailConfirmToken)
        {
            var emailConfirmation = await _accountRepository.GetEmailConfirmationByIdAsync(Guid.Parse(EmailConfirmToken));
            if(emailConfirmation == null)
            {
                return Result.Fail("Invalid email confirmation token.");
            }
            var user = await _userManager.FindByIdAsync(emailConfirmation.UserId);
            if(user == null)
            {
                return Result.Fail("User not found.");
            }
            emailConfirmation.ExpirationTime = DateTime.UtcNow.AddMinutes(10);
            emailConfirmation.Code = GenerateConfirmationCode().ToString();
            await _accountRepository.UpdateEmailConfirmationCodeAsync(emailConfirmation);
            var res = await _emailService.SendEmailAsync(emailConfirmation.Email, "Email Confirmation Code", $"Your confirmation code is: {emailConfirmation.Code}");
            if(res.IsFailed)
            {
                return Result.Fail("Failed to send email. : {}" + res.Errors.FirstOrDefault()?.Message);
            }
            return Result.Ok();
        }

        public async Task<Result<AccountDto>> GetUserData()
        {
            var userId = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            var userEntity = await _redis.StringGetAsync($"userEntity:{userId}");
            if (!userEntity.IsNullOrEmpty)            {
                var cachedUser = Newtonsoft.Json.JsonConvert.DeserializeObject<CachedAccountData>(userEntity!);
                if (cachedUser != null)                {
                    var accountredisDto = new AccountDto();
                    accountredisDto.UserSettings = cachedUser.UserSettings;
                    accountredisDto.Email = cachedUser.Email;
                    accountredisDto.EmailConfirmed = cachedUser.EmailConfirmed;
                    return Result.Ok(accountredisDto);
                }
            }
            if (string.IsNullOrEmpty(userId))
            { 
                return Result.Fail("User is not authenticated.");
            }
            var user = await _context.Users.Include(u => u.UserSettings).ThenInclude(u=>u.Exchanges).ThenInclude(e => e.Exchange).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Result.Fail("User not found.");
            }
            var accountDto = new AccountDto();
            accountDto.UserSettings = user.UserSettings;
            accountDto.Email = user.Email;
            accountDto.EmailConfirmed = user.EmailConfirmed;
            await _redis.StringSetAsync($"userEntity:{user.Id}", SerializeCachedAccount(user), TimeSpan.FromHours(1));
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

        /// <summary>
        /// Generates a new refresh token for the given user.
        /// </summary>
        /// <param name="userId">The user ID to generate the token for.</param>
        /// <param name="ipAddress">The IP address where the token is being created.</param>
        /// <param name="userAgent">The user agent of the requesting client.</param>
        /// <param name="deviceId">Optional device identifier for session management.</param>
        /// <returns>The generated refresh token model.</returns>
        public async Task<RefreshTokenModel> GenerateRefreshToken(string userId, string? ipAddress = null, string? userAgent = null, string? deviceId = null)
        {
            // Generate a cryptographically secure random token
            var tokenBytes = new byte[64];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }
            var token = Convert.ToBase64String(tokenBytes);
            var tokenHash = HashToken(token);

            var refreshToken = new RefreshTokenModel
            {
                UserId = userId,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress,
                UserAgent = userAgent,
                DeviceId = deviceId
            };

            await _accountRepository.AddRefreshTokenAsync(refreshToken, detachAfterSave: true);

            // Preserve plaintext token only for response payload.
            refreshToken.TokenHash = token; // safe: entity is no longer tracked
            return refreshToken;
        }

        /// <summary>
        /// Validates a refresh token and checks if it is still active.
        /// </summary>
        /// <param name="userId">The user ID that owns the token.</param>
        /// <param name="token">The refresh token to validate.</param>
        /// <returns>The refresh token record if valid, null otherwise.</returns>
        public async Task<RefreshTokenModel?> ValidateRefreshToken(string userId, string token)
        {
            var tokenHash = HashToken(token);

            var refreshToken = await _accountRepository.GetRefreshTokenByUserAndHashAsync(userId, tokenHash);

            // Token not found
            if (refreshToken == null)
                return null;

            // Token has expired
            if (refreshToken.ExpiresAt <= DateTime.UtcNow)
                return null;

            // Token has been revoked
            if (refreshToken.RevokedAt.HasValue)
                return null;

            return refreshToken;
        }

        /// <summary>
        /// Validates a refresh token and rotates it if valid.
        /// On rotation: revokes old token, generates new token, stores replacement link.
        /// On invalid/reused token: revokes entire token family.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="token">The refresh token.</param>
        /// <param name="ipAddress">The IP address making the request.</param>
        /// <param name="userAgent">The user agent of the client.</param>
        /// <returns>New refresh token if valid, null if invalid/reused.</returns>
        public async Task<RefreshTokenModel?> ValidateAndRotateRefreshToken(string userId, string token, string? ipAddress = null, string? userAgent = null)
        {
            var tokenHash = HashToken(token);
            var refreshToken = await _accountRepository.GetRefreshTokenByUserAndHashAsync(userId, tokenHash);

            // Token not found — the token is unknown for this user (expired, already cleaned up,
            // or from a different session). Return null without revoking valid tokens: an unknown
            // token cannot be traced to a token family, so a mass-revocation would incorrectly
            // invalidate legitimate sessions (e.g. a second browser tab that rotated first).
            if (refreshToken == null)
            {
                return null;
            }

            // Token has been revoked or has a replacement - suspicious reuse
            if (refreshToken.RevokedAt.HasValue || refreshToken.ReplacedByTokenId.HasValue)
            {
                // Revoke the entire token family
                await RevokeTokenChain(refreshToken, "suspicious_reuse_detected", ipAddress);
                return null;
            }

            // Token has expired
            if (refreshToken.ExpiresAt <= DateTime.UtcNow)
                return null;

            // Valid token - revoke it and issue a new one
            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevocationReason = "token_rotated";
            refreshToken.RevokedByIp = ipAddress;

            var newToken = await GenerateRefreshToken(userId, ipAddress, userAgent);
            
            // Link the new token to the old one (replacement chain)
            refreshToken.ReplacedByTokenId = newToken.Id;
            await _accountRepository.UpdateRefreshTokenAsync(refreshToken);

            return newToken;
        }

        /// <summary>
        /// Revokes a specific refresh token (used for logout).
        /// </summary>
        /// <param name="tokenId">The ID of the token to revoke.</param>
        /// <param name="ipAddress">The IP address where revocation was requested.</param>
        /// <returns>True if revoked, false if token not found.</returns>
        public async Task<bool> RevokeToken(Guid tokenId, string? ipAddress = null)
        {
            var refreshToken = await _accountRepository.GetRefreshTokenByIdAsync(tokenId);
            if (refreshToken == null)
                return false;

            if (refreshToken.IsRevoked)
                return true; // Already revoked

            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevocationReason = "logout";
            refreshToken.RevokedByIp = ipAddress;

            await _accountRepository.UpdateRefreshTokenAsync(refreshToken);

            return true;
        }

        /// <summary>
        /// Revokes an entire token chain starting from a given token (for suspicious activity).
        /// Follows the replacement chain back to the root and revokes all in the chain.
        /// </summary>
        /// <param name="token">The token to start the revocation chain from.</param>
        /// <param name="reason">The reason for revocation.</param>
        /// <param name="ipAddress">The IP address where revocation was triggered.</param>
        public async Task RevokeTokenChain(RefreshTokenModel token, string reason, string? ipAddress = null)
        {
            var tokenToRevoke = token;

            // Revoke current and all related tokens in the chain
            var tokensInChain = new List<RefreshTokenModel> { token };

            // Find the root token by walking back through the chain (if needed)
            // For now, just revoke the given token and any that reference it

            tokenToRevoke.RevokedAt = DateTime.UtcNow;
            tokenToRevoke.RevocationReason = reason;
            tokenToRevoke.RevokedByIp = ipAddress;
            await _accountRepository.UpdateRefreshTokenAsync(tokenToRevoke);

            // Also revoke any tokens that replaced this one
            var replacedTokens = await _accountRepository.GetRefreshTokensByReplacedByTokenIdAsync(tokenToRevoke.Id);

            foreach (var replacedToken in replacedTokens)
            {
                replacedToken.RevokedAt = DateTime.UtcNow;
                replacedToken.RevocationReason = reason;
                replacedToken.RevokedByIp = ipAddress;
                await _accountRepository.UpdateRefreshTokenAsync(replacedToken);
            }
        }

        /// <summary>
        /// Revokes all active refresh tokens for a user (global logout/security breach).
        /// </summary>
        /// <param name="userId">The user ID whose tokens should be revoked.</param>
        /// <param name="reason">The reason for revoking all tokens.</param>
        /// <param name="ipAddress">The IP address where the revocation was requested.</param>
        public async Task RevokeAllUserTokens(string userId, string reason, string? ipAddress = null)
        {
            var activeTokens = await _accountRepository.GetActiveRefreshTokensForUserAsync(userId, DateTime.UtcNow);

            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevocationReason = reason;
                token.RevokedByIp = ipAddress;
                await _accountRepository.UpdateRefreshTokenAsync(token);
            }
        }

        /// <summary>
        /// Hashes a refresh token using SHA256 for secure storage.
        /// </summary>
        /// <param name="token">The token to hash.</param>
        /// <returns>Base64-encoded SHA256 hash of the token.</returns>
        private string HashToken(string token)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hashedBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        /// <summary>
        /// Refreshes an access token by resolving user from refresh token.
        /// </summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <param name="ipAddress">The IP address of the request.</param>
        /// <param name="userAgent">The user agent of the request.</param>
        /// <returns>RefreshTokenResponse with new tokens or error.</returns>
        public async Task<Result<RefreshTokenResponse>> RefreshAccessToken(string refreshToken, string? ipAddress = null, string? userAgent = null)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return Result.Fail("Refresh token is required.");
            }

            var tokenHash = HashToken(refreshToken);
            var tokenEntity = await _accountRepository.GetRefreshTokenByHashAsync(tokenHash);

            if (tokenEntity == null)
            {
                return Result.Fail("Invalid or expired refresh token.");
            }

            return await RefreshAccessToken(tokenEntity.UserId, refreshToken, ipAddress, userAgent);
        }

        /// <summary>
        /// Refreshes an access token using a valid refresh token.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="refreshToken">The refresh token.</param>
        /// <param name="ipAddress">The IP address of the request.</param>
        /// <param name="userAgent">The user agent of the request.</param>
        /// <returns>RefreshTokenResponse with new tokens or error.</returns>
        public async Task<Result<RefreshTokenResponse>> RefreshAccessToken(string userId, string refreshToken, string? ipAddress = null, string? userAgent = null)
        {
            try
            {
                ipAddress ??= GetClientIpAddress();
                userAgent ??= _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();

                // Validate and rotate the refresh token
                var rotatedToken = await ValidateAndRotateRefreshToken(userId, refreshToken, ipAddress, userAgent);
                if (rotatedToken == null)
                {
                    return Result.Fail("Invalid or expired refresh token.");
                }

                // Generate new access token
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Result.Fail("User not found.");
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimsIdentity.DefaultNameClaimType, user.Id.ToString())
                };
                var claimsIdentity = new ClaimsIdentity(claims, "Token", ClaimsIdentity.DefaultNameClaimType, ClaimsIdentity.DefaultRoleClaimType);

                var now = DateTime.UtcNow;
                var expiresAt = now.Add(TimeSpan.FromMinutes(_jwtOptions.AccessTokenExpirationMinutes));
                var jwt = new JwtSecurityToken(
                    issuer: _jwtOptions.Issuer,
                    audience: _jwtOptions.Audience,
                    notBefore: now,
                    claims: claimsIdentity.Claims,
                    expires: expiresAt,
                    signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_jwtOptions.SigningKey)), SecurityAlgorithms.HmacSha256));
                
                var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

                var response = new RefreshTokenResponse
                {
                    AccessToken = encodedJwt,
                    RefreshToken = rotatedToken.TokenHash, // Contains actual token value
                    ExpiresIn = new DateTimeOffset(expiresAt).ToUnixTimeSeconds()
                };

                return Result.Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh failed for user {UserId}", userId);
                return Result.Fail($"Token refresh failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs out the user by revoking their refresh token.
        /// </summary>
        /// <param name="tokenId">The ID of the refresh token to revoke.</param>
        /// <param name="ipAddress">The IP address of the request.</param>
        /// <returns>Success result or error.</returns>
        public async Task<Result> Logout(Guid tokenId, string? ipAddress = null)
        {
            try
            {
                ipAddress ??= GetClientIpAddress();
                var success = await RevokeToken(tokenId, ipAddress);
                
                if (success)
                {
                    return Result.Ok();
                }
                else
                {
                    return Result.Fail("Token not found or already revoked.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout failed for token {TokenId}", tokenId);
                return Result.Fail($"Logout failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs out the user by revoking a refresh token using its value.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="refreshToken">The refresh token value to revoke.</param>
        /// <param name="ipAddress">The IP address of the request.</param>
        /// <returns>Success result or error.</returns>
        public async Task<Result> LogoutByToken(string userId, string refreshToken, string? ipAddress = null)
        {
            try
            {
                ipAddress ??= GetClientIpAddress();
                var success = await RevokeTokenByValue(userId, refreshToken, ipAddress);
                
                if (success)
                {
                    return Result.Ok();
                }
                else
                {
                    return Result.Fail("Token not found or already revoked.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout failed for user {UserId}", userId);
                return Result.Fail($"Logout failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Revokes a refresh token by its value (for logout).
        /// Finds the token and marks it as revoked.
        /// </summary>
        /// <param name="userId">The user ID that owns the token.</param>
        /// <param name="tokenValue">The refresh token value.</param>
        /// <param name="ipAddress">The IP address where revocation was requested.</param>
        /// <returns>True if revoked, false if token not found or already revoked.</returns>
        public async Task<bool> RevokeTokenByValue(string userId, string tokenValue, string? ipAddress = null)
        {
            var tokenHash = HashToken(tokenValue);
            var refreshToken = await _accountRepository.GetRefreshTokenByUserAndHashAsync(userId, tokenHash);

            if (refreshToken == null)
                return false;

            if (refreshToken.IsRevoked)
                return true; // Already revoked

            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevocationReason = "logout";
            refreshToken.RevokedByIp = ipAddress;

            await _accountRepository.UpdateRefreshTokenAsync(refreshToken);

            return true;
        }

        /// <summary>
        /// Gets the client IP address from the HTTP context.
        /// Handles X-Forwarded-For header for proxy scenarios.
        /// </summary>
        /// <returns>The client IP address or null if unavailable.</returns>
        private string? GetClientIpAddress()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
                return null;

            // Try X-Forwarded-For header first (for proxied requests)
            var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                return xForwardedFor.Split(',')[0].Trim();
            }

            // Fall back to RemoteIpAddress
            return context.Connection.RemoteIpAddress?.ToString();
        }
    }
}
