using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.DbContext;
using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class AccountServiceTests
{
    private readonly Mock<UserManager<AccountModel>> _userManager;
    private readonly Mock<SignInManager<AccountModel>> _signInManager;
    private readonly Mock<IAccountRepository> _repo;
    private readonly Mock<IEmailService> _email;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly Mock<IDatabase> _redisDb;
    private readonly AppDbContext _dbContext;
    private readonly AccountService _sut;

    public AccountServiceTests()
    {
        _userManager = MockHelpers.CreateUserManagerMock();
        _signInManager = MockHelpers.CreateSignInManagerMock(_userManager);
        Mock<IConnectionMultiplexer> redis;
        (redis, _redisDb) = MockHelpers.CreateRedisMocks();
        _redisDb.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _repo = new Mock<IAccountRepository>();
        _email = new Mock<IEmailService>();
        _dbContext = MockHelpers.CreateInMemoryDbContext();

        var config = new Mock<IConfiguration>();
        config.Setup(c => c["ClientUrl"]).Returns("http://localhost:3001");

        var accountServiceContext = new AccountServiceContext(
            _signInManager.Object,
            _userManager.Object,
            _httpContextAccessor.Object,
            config.Object);

        _sut = new AccountService(
            accountServiceContext,
            redis.Object,
            _dbContext,
            _repo.Object,
            _email.Object,
            Options.Create(MockHelpers.CreateTestJwtOptions()),
            NullLogger<AccountService>.Instance);
    }

    private void SetupAuthenticatedUser(string userId)
    {
        var identity = new Mock<System.Security.Principal.IIdentity>();
        identity.Setup(i => i.Name).Returns(userId);
        var principal = new Mock<System.Security.Claims.ClaimsPrincipal>();
        principal.Setup(p => p.Identity).Returns(identity.Object);
        var ctx = new DefaultHttpContext();
        var wrapped = new Mock<HttpContext>();
        wrapped.Setup(c => c.User).Returns(principal.Object);
        wrapped.Setup(c => c.Request).Returns(ctx.Request);
        wrapped.Setup(c => c.Connection).Returns(ctx.Connection);
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(wrapped.Object);
    }

    private void SetupUnauthenticated() => _httpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

    [Fact]
    public async Task CheckConfirmationCodeEmail_EmptyId_ReturnsFail()
    {
        var result = await _sut.CheckConfirmationCodeEmail(string.Empty, "123456");
        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckConfirmationCodeEmail_CodeNotFound_ReturnsFail()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetEmailConfirmationByIdAsync(id)).ReturnsAsync((EmailConfirmationCodes?)null);

        var result = await _sut.CheckConfirmationCodeEmail(id.ToString(), "123456");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckConfirmationCodeEmail_CodeExpired_ReturnsFail()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetEmailConfirmationByIdAsync(id)).ReturnsAsync(new EmailConfirmationCodes
        {
            Id = id, UserId = "u1", Email = "a@b.com",
            Code = "123456", ExpirationTime = DateTime.UtcNow.AddMinutes(-1)
        });

        var result = await _sut.CheckConfirmationCodeEmail(id.ToString(), "123456");

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("expired");
    }

    [Fact]
    public async Task CheckConfirmationCodeEmail_WrongCode_ReturnsFail()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetEmailConfirmationByIdAsync(id)).ReturnsAsync(new EmailConfirmationCodes
        {
            Id = id, UserId = "u1", Email = "a@b.com",
            Code = "111111", ExpirationTime = DateTime.UtcNow.AddMinutes(10)
        });

        var result = await _sut.CheckConfirmationCodeEmail(id.ToString(), "999999");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckConfirmationCodeEmail_ValidCode_ConfirmsEmailAndReturnsOk()
    {
        var id = Guid.NewGuid();
        var confirmation = new EmailConfirmationCodes
        {
            Id = id, UserId = "u1", Email = "new@b.com",
            Code = "123456", ExpirationTime = DateTime.UtcNow.AddMinutes(10)
        };
        var user = new AccountModel { Id = "u1" };

        _repo.Setup(r => r.GetEmailConfirmationByIdAsync(id)).ReturnsAsync(confirmation);
        _userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(user);
        _userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _repo.Setup(r => r.RemoveEmailConfirmationCodeAsync(confirmation)).Returns(Task.CompletedTask);

        var result = await _sut.CheckConfirmationCodeEmail(id.ToString(), "123456");

        result.IsSuccess.Should().BeTrue();
        user.EmailConfirmed.Should().BeTrue();
        user.Email.Should().Be("new@b.com");
    }

    [Fact]
    public async Task SendForgetPasswordCode_UserNotFound_ReturnsFail()
    {
        _userManager.Setup(m => m.FindByEmailAsync("x@x.com")).ReturnsAsync((AccountModel?)null);

        var result = await _sut.SendForgetPasswordCode("x@x.com");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SendForgetPasswordCode_EmailSendFails_ReturnsFail()
    {
        var user = new AccountModel { Id = "u1", Email = "a@b.com" };
        _userManager.Setup(m => m.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-tok");
        _repo.Setup(r => r.ReplaceForgetPasswordRequestAsync(It.IsAny<string>(), It.IsAny<ForgetPasswordRequest>()))
            .Returns(Task.CompletedTask);
        _email.Setup(e => e.SendEmailAsync(user.Email, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(FluentResults.Result.Fail("smtp error"));

        var result = await _sut.SendForgetPasswordCode(user.Email);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SendForgetPasswordCode_Success_StoresRequestAndReturnsOk()
    {
        var user = new AccountModel { Id = "u1", Email = "a@b.com" };
        _userManager.Setup(m => m.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-tok");
        _repo.Setup(r => r.ReplaceForgetPasswordRequestAsync(It.IsAny<string>(), It.IsAny<ForgetPasswordRequest>()))
            .Returns(Task.CompletedTask);
        _email.Setup(e => e.SendEmailAsync(user.Email, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(FluentResults.Result.Ok());

        var result = await _sut.SendForgetPasswordCode(user.Email);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.ReplaceForgetPasswordRequestAsync("u1", It.IsAny<ForgetPasswordRequest>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_TokenNotFound_ReturnsFail()
    {
        var tokenId = Guid.NewGuid();
        _repo.Setup(r => r.GetForgetPasswordRequestByIdAsync(tokenId))
            .ReturnsAsync((ForgetPasswordRequest?)null);

        var result = await _sut.ResetPassword(new ChangePasswordDto
        {
            Token = tokenId.ToString(),
            NewPassword = "NewPass1!"
        });

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_UserNotFound_ReturnsFail()
    {
        var tokenId = Guid.NewGuid();
        _repo.Setup(r => r.GetForgetPasswordRequestByIdAsync(tokenId))
            .ReturnsAsync(new ForgetPasswordRequest { Id = tokenId, UserId = "u1", Token = "identity-tok" });
        _userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync((AccountModel?)null);

        var result = await _sut.ResetPassword(new ChangePasswordDto
        {
            Token = tokenId.ToString(),
            NewPassword = "NewPass1!"
        });

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_Success_ReturnsOk()
    {
        var tokenId = Guid.NewGuid();
        var user = new AccountModel { Id = "u1" };
        _repo.Setup(r => r.GetForgetPasswordRequestByIdAsync(tokenId))
            .ReturnsAsync(new ForgetPasswordRequest { Id = tokenId, UserId = "u1", Token = "identity-tok" });
        _userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(user);
        _userManager.Setup(m => m.ResetPasswordAsync(user, "identity-tok", "NewPass1!"))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.ResetPassword(new ChangePasswordDto
        {
            Token = tokenId.ToString(),
            NewPassword = "NewPass1!"
        });

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAndRotateRefreshToken_TokenNotFound_ReturnsNull()
    {
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((RefreshTokenModel?)null);

        var result = await _sut.ValidateAndRotateRefreshToken("u1", "tok");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndRotateRefreshToken_TokenAlreadyRevoked_RevokesChainAndReturnsNull()
    {
        var existing = new RefreshTokenModel
        {
            Id = Guid.NewGuid(), UserId = "u1",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = DateTime.UtcNow
        };
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(existing);
        _repo.Setup(r => r.UpdateRefreshTokenAsync(It.IsAny<RefreshTokenModel>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.GetRefreshTokensByReplacedByTokenIdAsync(existing.Id))
            .ReturnsAsync(new List<RefreshTokenModel>());

        var result = await _sut.ValidateAndRotateRefreshToken("u1", "tok");

        result.Should().BeNull();
        _repo.Verify(r => r.UpdateRefreshTokenAsync(It.IsAny<RefreshTokenModel>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ValidateAndRotateRefreshToken_TokenExpired_ReturnsNull()
    {
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new RefreshTokenModel
            {
                UserId = "u1",
                ExpiresAt = DateTime.UtcNow.AddDays(-1)
            });

        var result = await _sut.ValidateAndRotateRefreshToken("u1", "tok");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndRotateRefreshToken_ValidToken_RevokesOldAndReturnsNewToken()
    {
        var existing = new RefreshTokenModel
        {
            Id = Guid.NewGuid(), UserId = "u1",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(existing);
        _repo.Setup(r => r.AddRefreshTokenAsync(It.IsAny<RefreshTokenModel>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.UpdateRefreshTokenAsync(It.IsAny<RefreshTokenModel>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.ValidateAndRotateRefreshToken("u1", "tok");

        result.Should().NotBeNull();
        existing.RevokedAt.Should().NotBeNull();
        existing.RevocationReason.Should().Be("token_rotated");
        existing.ReplacedByTokenId.Should().Be(result!.Id);
    }

    [Fact]
    public async Task RevokeToken_NotFound_ReturnsFalse()
    {
        _repo.Setup(r => r.GetRefreshTokenByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((RefreshTokenModel?)null);

        var ok = await _sut.RevokeToken(Guid.NewGuid());

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeToken_AlreadyRevoked_ReturnsTrueWithoutUpdate()
    {
        _repo.Setup(r => r.GetRefreshTokenByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new RefreshTokenModel { RevokedAt = DateTime.UtcNow });

        var ok = await _sut.RevokeToken(Guid.NewGuid());

        ok.Should().BeTrue();
        _repo.Verify(r => r.UpdateRefreshTokenAsync(It.IsAny<RefreshTokenModel>()), Times.Never);
    }

    [Fact]
    public async Task RevokeToken_ValidToken_RevokesWithIpAndReturnsTrue()
    {
        var token = new RefreshTokenModel { Id = Guid.NewGuid(), ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _repo.Setup(r => r.GetRefreshTokenByIdAsync(token.Id)).ReturnsAsync(token);
        _repo.Setup(r => r.UpdateRefreshTokenAsync(token)).Returns(Task.CompletedTask);

        var ok = await _sut.RevokeToken(token.Id, "1.2.3.4");

        ok.Should().BeTrue();
        token.RevokedAt.Should().NotBeNull();
        token.RevocationReason.Should().Be("logout");
        token.RevokedByIp.Should().Be("1.2.3.4");
    }

    [Fact]
    public async Task RevokeAllUserTokens_RevokesEveryActiveToken()
    {
        var t1 = new RefreshTokenModel { Id = Guid.NewGuid(), UserId = "u1", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        var t2 = new RefreshTokenModel { Id = Guid.NewGuid(), UserId = "u1", ExpiresAt = DateTime.UtcNow.AddDays(2) };

        _repo.Setup(r => r.GetActiveRefreshTokensForUserAsync("u1", It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RefreshTokenModel> { t1, t2 });
        _repo.Setup(r => r.UpdateRefreshTokenAsync(It.IsAny<RefreshTokenModel>())).Returns(Task.CompletedTask);

        await _sut.RevokeAllUserTokens("u1", "security_breach", "10.0.0.1");

        _repo.Verify(r =>
            r.UpdateRefreshTokenAsync(It.Is<RefreshTokenModel>(t => t.RevocationReason == "security_breach")),
            Times.Exactly(2));
        t1.RevokedAt.Should().NotBeNull();
        t2.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_UserNotFound_ReturnsFail()
    {
        _userManager.Setup(m => m.FindByNameAsync("nobody")).ReturnsAsync((AccountModel?)null);

        var result = await _sut.Login(new AccountLoginDto { Login = "nobody", Password = "pw" });

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Login_NotAllowedAndEmailUnconfirmed_ReturnsUnconfirmedDto()
    {
        var user = new AccountModel { Id = "u1", Email = "a@b.com", EmailConfirmed = false };
        _userManager.Setup(m => m.FindByNameAsync("a@b.com")).ReturnsAsync(user);
        _signInManager.Setup(s => s.CheckPasswordSignInAsync(user, "pw", false)).ReturnsAsync(SignInResult.NotAllowed);
        _repo.Setup(r => r.GetEmailConfirmationByUserIdAsync("u1"))
            .ReturnsAsync(new EmailConfirmationCodes { Id = Guid.NewGuid(), UserId = "u1" });

        var result = await _sut.Login(new AccountLoginDto { Login = "a@b.com", Password = "pw" });

        result.IsSuccess.Should().BeTrue();
        result.Value.EmailConfirmed.Should().BeFalse();
        result.Value.EmailConfirmToken.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Login_NotAllowedAndEmailConfirmed_ReturnsForbidden()
    {
        var user = new AccountModel { Id = "u1", Email = "a@b.com", EmailConfirmed = true };
        _userManager.Setup(m => m.FindByNameAsync("a@b.com")).ReturnsAsync(user);
        _signInManager.Setup(s => s.CheckPasswordSignInAsync(user, "pw", false)).ReturnsAsync(SignInResult.NotAllowed);

        var result = await _sut.Login(new AccountLoginDto { Login = "a@b.com", Password = "pw" });

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var user = new AccountModel { Id = "u1", Email = "a@b.com" };
        _userManager.Setup(m => m.FindByNameAsync("a@b.com")).ReturnsAsync(user);
        _signInManager.Setup(s => s.CheckPasswordSignInAsync(user, "wrong", false)).ReturnsAsync(SignInResult.Failed);

        var result = await _sut.Login(new AccountLoginDto { Login = "a@b.com", Password = "wrong" });

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Login_Success_ReturnsAccountDtoWithTokens()
    {
        var user = new AccountModel { Id = "u1", Email = "a@b.com", EmailConfirmed = true, UserSettingsId = 1 };
        _userManager.Setup(m => m.FindByNameAsync("a@b.com")).ReturnsAsync(user);
        _signInManager.Setup(s => s.CheckPasswordSignInAsync(user, "pw", false)).ReturnsAsync(SignInResult.Success);
        _repo.Setup(r => r.AddRefreshTokenAsync(It.IsAny<RefreshTokenModel>(), true)).Returns(Task.CompletedTask);

        var result = await _sut.Login(new AccountLoginDto { Login = "a@b.com", Password = "pw" });

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeEmpty();
        result.Value.RefreshToken.Should().NotBeEmpty();
        result.Value.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterUser_EmailAlreadyExists_ReturnsFail()
    {
        _userManager.Setup(m => m.FindByEmailAsync("a@b.com")).ReturnsAsync(new AccountModel { Id = "existing" });

        var result = await _sut.RegisterUser(new AccountLoginDto { Login = "a@b.com", Password = "pw" });

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterUser_CreateFails_ReturnsValidationFail()
    {
        _userManager.Setup(m => m.FindByEmailAsync("a@b.com")).ReturnsAsync((AccountModel?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<AccountModel>(), "pw"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        var result = await _sut.RegisterUser(new AccountLoginDto { Login = "a@b.com", Password = "pw" });

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("Password too weak");
    }

    [Fact]
    public async Task RegisterUser_CreateSucceedsButEmailFails_ReturnsFail()
    {
        _userManager.Setup(m => m.FindByEmailAsync("a@b.com")).ReturnsAsync((AccountModel?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<AccountModel>(), "pw")).ReturnsAsync(IdentityResult.Success);
        _email.Setup(e => e.SendEmailAsync("a@b.com", It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(FluentResults.Result.Fail("smtp down"));

        var result = await _sut.RegisterUser(new AccountLoginDto { Login = "a@b.com", Password = "pw" });

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("confirmation email");
    }

    [Fact]
    public async Task RegisterUser_Success_ReturnsEmailConfirmationCode()
    {
        _userManager.Setup(m => m.FindByEmailAsync("a@b.com")).ReturnsAsync((AccountModel?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<AccountModel>(), "pw")).ReturnsAsync(IdentityResult.Success);
        _email.Setup(e => e.SendEmailAsync("a@b.com", It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(FluentResults.Result.Ok());
        _repo.Setup(r => r.ReplaceEmailConfirmationCodeAsync(It.IsAny<string>(), It.IsAny<EmailConfirmationCodes>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.RegisterUser(new AccountLoginDto { Login = "a@b.com", Password = "pw" });

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("a@b.com");
    }

    [Fact]
    public async Task RegisterUser_UserManagerThrows_ReturnsFail()
    {
        _userManager.Setup(m => m.FindByEmailAsync("a@b.com")).ThrowsAsync(new Exception("db down"));

        var result = await _sut.RegisterUser(new AccountLoginDto { Login = "a@b.com", Password = "pw" });

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("Registration failed");
    }

    [Fact]
    public async Task CheckConfirmationCodeEmail_UserNotFound_ReturnsFail()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetEmailConfirmationByIdAsync(id)).ReturnsAsync(new EmailConfirmationCodes
        {
            Id = id, UserId = "missing", Email = "a@b.com", Code = "123456", ExpirationTime = DateTime.UtcNow.AddMinutes(10)
        });
        _userManager.Setup(m => m.FindByIdAsync("missing")).ReturnsAsync((AccountModel?)null);

        var result = await _sut.CheckConfirmationCodeEmail(id.ToString(), "123456");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckConfirmationCodeEmail_ExceptionThrown_ReturnsFail()
    {
        _repo.Setup(r => r.GetEmailConfirmationByIdAsync(It.IsAny<Guid>())).ThrowsAsync(new Exception("boom"));

        var result = await _sut.CheckConfirmationCodeEmail(Guid.NewGuid().ToString(), "123456");

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("Email confirmation failed");
    }

    [Fact]
    public async Task UpdateDetails_Unauthenticated_ReturnsFail()
    {
        SetupUnauthenticated();

        var result = await _sut.UpdateDetails(new AccountEditDto());

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateDetails_UserNotFound_ReturnsFail()
    {
        SetupAuthenticatedUser("missing");

        var result = await _sut.UpdateDetails(new AccountEditDto());

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateDetails_Success_UpdatesSettingsAndExchanges()
    {
        SetupAuthenticatedUser("acc1");
        var exchange = new ExchangeModel { Id = 1, Name = "binance" };
        _dbContext.Exchanges.Add(exchange);
        var settings = new UserSettingsModel { Id = 1, AccountId = "acc1" };
        var account = new AccountModel { Id = "acc1", UserSettingsId = 1, UserSettings = settings };
        _dbContext.Users.Add(account);
        await _dbContext.SaveChangesAsync();

        var editDto = new AccountEditDto
        {
            SpreadSize = 5,
            PositionSize = 20,
            FuturesSpread = true,
            Exchanges = new List<UserExchangeModel> { new() { Exchange = exchange } }
        };

        var result = await _sut.UpdateDetails(editDto);

        result.IsSuccess.Should().BeTrue();
        var reloaded = await _dbContext.UsersSettings.FindAsync(1);
        reloaded!.SpreadSize.Should().Be(5);
        reloaded.PositionSize.Should().Be(20);
        reloaded.FuturesSpread.Should().BeTrue();
    }

    [Fact]
    public async Task SendConfirmationEmail_EmailSendFails_ReturnsFail()
    {
        _email.Setup(e => e.SendEmailAsync("a@b.com", It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(FluentResults.Result.Fail("smtp down"));

        var result = await _sut.SendConfirmationEmail("a@b.com", new AccountModel { Id = "u1" });

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeEmailRequest_Unauthenticated_ReturnsFail()
    {
        SetupUnauthenticated();

        var result = await _sut.ChangeEmailRequest("new@b.com");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeEmailRequest_UserNotFound_ReturnsFail()
    {
        SetupAuthenticatedUser("u1");
        _userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync((AccountModel?)null);

        var result = await _sut.ChangeEmailRequest("new@b.com");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeEmailRequest_EmailTakenByAnotherUser_ReturnsFail()
    {
        SetupAuthenticatedUser("u1");
        _userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(new AccountModel { Id = "u1" });
        _userManager.Setup(m => m.FindByEmailAsync("new@b.com")).ReturnsAsync(new AccountModel { Id = "u2" });

        var result = await _sut.ChangeEmailRequest("new@b.com");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeEmailRequest_EmailBelongsToSameUser_SendsConfirmation()
    {
        SetupAuthenticatedUser("u1");
        var user = new AccountModel { Id = "u1" };
        _userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(user);
        _userManager.Setup(m => m.FindByEmailAsync("new@b.com")).ReturnsAsync(user);
        _email.Setup(e => e.SendEmailAsync("new@b.com", It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(FluentResults.Result.Ok());
        _repo.Setup(r => r.ReplaceEmailConfirmationCodeAsync(It.IsAny<string>(), It.IsAny<EmailConfirmationCodes>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.ChangeEmailRequest("new@b.com");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeEmailRequest_SendFails_ReturnsFail()
    {
        SetupAuthenticatedUser("u1");
        var user = new AccountModel { Id = "u1" };
        _userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(user);
        _userManager.Setup(m => m.FindByEmailAsync("new@b.com")).ReturnsAsync((AccountModel?)null);
        _email.Setup(e => e.SendEmailAsync("new@b.com", It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(FluentResults.Result.Fail("smtp down"));

        var result = await _sut.ChangeEmailRequest("new@b.com");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ResendEmailConfirmation_TokenNotFound_ReturnsFail()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetEmailConfirmationByIdAsync(id)).ReturnsAsync((EmailConfirmationCodes?)null);

        var result = await _sut.ResendEmailConfirmation(id.ToString());

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ResendEmailConfirmation_UserNotFound_ReturnsFail()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetEmailConfirmationByIdAsync(id))
            .ReturnsAsync(new EmailConfirmationCodes { Id = id, UserId = "missing", Email = "a@b.com" });
        _userManager.Setup(m => m.FindByIdAsync("missing")).ReturnsAsync((AccountModel?)null);

        var result = await _sut.ResendEmailConfirmation(id.ToString());

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ResendEmailConfirmation_SendFails_ReturnsFail()
    {
        var id = Guid.NewGuid();
        var confirmation = new EmailConfirmationCodes { Id = id, UserId = "u1", Email = "a@b.com" };
        _repo.Setup(r => r.GetEmailConfirmationByIdAsync(id)).ReturnsAsync(confirmation);
        _userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(new AccountModel { Id = "u1" });
        _repo.Setup(r => r.UpdateEmailConfirmationCodeAsync(confirmation)).Returns(Task.CompletedTask);
        _email.Setup(e => e.SendEmailAsync("a@b.com", It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(FluentResults.Result.Fail("smtp down"));

        var result = await _sut.ResendEmailConfirmation(id.ToString());

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ResendEmailConfirmation_Success_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var confirmation = new EmailConfirmationCodes { Id = id, UserId = "u1", Email = "a@b.com" };
        _repo.Setup(r => r.GetEmailConfirmationByIdAsync(id)).ReturnsAsync(confirmation);
        _userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(new AccountModel { Id = "u1" });
        _repo.Setup(r => r.UpdateEmailConfirmationCodeAsync(confirmation)).Returns(Task.CompletedTask);
        _email.Setup(e => e.SendEmailAsync("a@b.com", It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(FluentResults.Result.Ok());

        var result = await _sut.ResendEmailConfirmation(id.ToString());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserData_CachedEntryPresent_ReturnsFromCache()
    {
        SetupAuthenticatedUser("u1");
        var cached = """{"UserSettings":{"AccountId":"u1"},"Email":"cached@b.com","EmailConfirmed":true}""";
        _redisDb.Setup(d => d.StringGetAsync((RedisKey)"userEntity:u1", It.IsAny<CommandFlags>())).ReturnsAsync((RedisValue)cached);

        var result = await _sut.GetUserData();

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("cached@b.com");
    }

    [Fact]
    public async Task GetUserData_NoCacheAndUnauthenticated_ReturnsFail()
    {
        SetupUnauthenticated();

        var result = await _sut.GetUserData();

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserData_NoCacheUserNotFound_ReturnsFail()
    {
        SetupAuthenticatedUser("missing");

        var result = await _sut.GetUserData();

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserData_NoCacheUserFound_ReturnsDtoAndCaches()
    {
        SetupAuthenticatedUser("acc1");
        var settings = new UserSettingsModel { Id = 5, AccountId = "acc1" };
        var account = new AccountModel { Id = "acc1", UserSettingsId = 5, Email = "acc1@b.com", UserSettings = settings };
        _dbContext.Users.Add(account);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetUserData();

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("acc1@b.com");
        _redisDb.Verify(d => d.StringSetAsync((RedisKey)"userEntity:acc1", It.IsAny<RedisValue>(), It.IsAny<Expiration>(), It.IsAny<ValueCondition>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ValidateRefreshToken_TokenNotFound_ReturnsNull()
    {
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync("u1", It.IsAny<string>())).ReturnsAsync((RefreshTokenModel?)null);

        var result = await _sut.ValidateRefreshToken("u1", "tok");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateRefreshToken_Expired_ReturnsNull()
    {
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync("u1", It.IsAny<string>()))
            .ReturnsAsync(new RefreshTokenModel { ExpiresAt = DateTime.UtcNow.AddDays(-1) });

        var result = await _sut.ValidateRefreshToken("u1", "tok");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateRefreshToken_Revoked_ReturnsNull()
    {
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync("u1", It.IsAny<string>()))
            .ReturnsAsync(new RefreshTokenModel { ExpiresAt = DateTime.UtcNow.AddDays(1), RevokedAt = DateTime.UtcNow });

        var result = await _sut.ValidateRefreshToken("u1", "tok");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateRefreshToken_Valid_ReturnsToken()
    {
        var token = new RefreshTokenModel { ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync("u1", It.IsAny<string>())).ReturnsAsync(token);

        var result = await _sut.ValidateRefreshToken("u1", "tok");

        result.Should().BeSameAs(token);
    }

    [Fact]
    public async Task ValidateAndRotateRefreshToken_ReplacedTokensExist_RevokesEntireChain()
    {
        var existing = new RefreshTokenModel
        {
            Id = Guid.NewGuid(), UserId = "u1",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = DateTime.UtcNow
        };
        var replaced = new RefreshTokenModel { Id = Guid.NewGuid(), UserId = "u1" };
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(existing);
        _repo.Setup(r => r.UpdateRefreshTokenAsync(It.IsAny<RefreshTokenModel>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.GetRefreshTokensByReplacedByTokenIdAsync(existing.Id))
            .ReturnsAsync(new List<RefreshTokenModel> { replaced });

        var result = await _sut.ValidateAndRotateRefreshToken("u1", "tok");

        result.Should().BeNull();
        replaced.RevokedAt.Should().NotBeNull();
        replaced.RevocationReason.Should().Be("suspicious_reuse_detected");
    }

    [Fact]
    public async Task RefreshAccessToken_ByTokenOnly_EmptyToken_ReturnsFail()
    {
        var result = await _sut.RefreshAccessToken(string.Empty);

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAccessToken_ByTokenOnly_TokenNotFound_ReturnsFail()
    {
        _repo.Setup(r => r.GetRefreshTokenByHashAsync(It.IsAny<string>(), false)).ReturnsAsync((RefreshTokenModel?)null);

        var result = await _sut.RefreshAccessToken("some-token");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAccessToken_ByTokenOnly_DelegatesToUserIdOverload()
    {
        var stored = new RefreshTokenModel { Id = Guid.NewGuid(), UserId = "u1", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _repo.Setup(r => r.GetRefreshTokenByHashAsync(It.IsAny<string>(), false)).ReturnsAsync(stored);
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync("u1", It.IsAny<string>())).ReturnsAsync(stored);
        _repo.Setup(r => r.UpdateRefreshTokenAsync(It.IsAny<RefreshTokenModel>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.AddRefreshTokenAsync(It.IsAny<RefreshTokenModel>(), true)).Returns(Task.CompletedTask);
        _userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(new AccountModel { Id = "u1" });

        var result = await _sut.RefreshAccessToken("some-token");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAccessToken_ByUserId_RotationFails_ReturnsFail()
    {
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync("u1", It.IsAny<string>())).ReturnsAsync((RefreshTokenModel?)null);

        var result = await _sut.RefreshAccessToken(userId: "u1", refreshToken: "tok");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAccessToken_ByUserId_UserNotFound_ReturnsFail()
    {
        var stored = new RefreshTokenModel { Id = Guid.NewGuid(), UserId = "u1", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync("u1", It.IsAny<string>())).ReturnsAsync(stored);
        _repo.Setup(r => r.UpdateRefreshTokenAsync(It.IsAny<RefreshTokenModel>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.AddRefreshTokenAsync(It.IsAny<RefreshTokenModel>(), true)).Returns(Task.CompletedTask);
        _userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync((AccountModel?)null);

        var result = await _sut.RefreshAccessToken(userId: "u1", refreshToken: "tok");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAccessToken_ByUserId_ExceptionThrown_ReturnsFail()
    {
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync("u1", It.IsAny<string>())).ThrowsAsync(new Exception("db down"));

        var result = await _sut.RefreshAccessToken(userId: "u1", refreshToken: "tok");

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("Token refresh failed");
    }

    [Fact]
    public async Task Logout_TokenNotFound_ReturnsFail()
    {
        _repo.Setup(r => r.GetRefreshTokenByIdAsync(It.IsAny<Guid>())).ReturnsAsync((RefreshTokenModel?)null);

        var result = await _sut.Logout(Guid.NewGuid());

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Logout_Success_ReturnsOk()
    {
        var token = new RefreshTokenModel { Id = Guid.NewGuid() };
        _repo.Setup(r => r.GetRefreshTokenByIdAsync(token.Id)).ReturnsAsync(token);
        _repo.Setup(r => r.UpdateRefreshTokenAsync(token)).Returns(Task.CompletedTask);

        var result = await _sut.Logout(token.Id);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Logout_RepositoryThrows_ReturnsFail()
    {
        _repo.Setup(r => r.GetRefreshTokenByIdAsync(It.IsAny<Guid>())).ThrowsAsync(new Exception("db down"));

        var result = await _sut.Logout(Guid.NewGuid());

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("Logout failed");
    }

    [Fact]
    public async Task LogoutByToken_TokenNotFound_ReturnsFail()
    {
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync("u1", It.IsAny<string>())).ReturnsAsync((RefreshTokenModel?)null);

        var result = await _sut.LogoutByToken("u1", "tok");

        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task LogoutByToken_Success_ReturnsOk()
    {
        var token = new RefreshTokenModel();
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync("u1", It.IsAny<string>())).ReturnsAsync(token);
        _repo.Setup(r => r.UpdateRefreshTokenAsync(token)).Returns(Task.CompletedTask);

        var result = await _sut.LogoutByToken("u1", "tok");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LogoutByToken_RepositoryThrows_ReturnsFail()
    {
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync("u1", It.IsAny<string>())).ThrowsAsync(new Exception("db down"));

        var result = await _sut.LogoutByToken("u1", "tok");

        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("Logout failed");
    }

    [Fact]
    public async Task RevokeTokenByValue_AlreadyRevoked_ReturnsTrueWithoutUpdate()
    {
        _repo.Setup(r => r.GetRefreshTokenByUserAndHashAsync("u1", It.IsAny<string>()))
            .ReturnsAsync(new RefreshTokenModel { RevokedAt = DateTime.UtcNow });

        var ok = await _sut.RevokeTokenByValue("u1", "tok");

        ok.Should().BeTrue();
        _repo.Verify(r => r.UpdateRefreshTokenAsync(It.IsAny<RefreshTokenModel>()), Times.Never);
    }

    [Fact]
    public async Task Login_WithForwardedForHeader_UsesForwardedIpForRefreshToken()
    {
        var user = new AccountModel { Id = "u1", Email = "a@b.com", EmailConfirmed = true, UserSettingsId = 1 };
        _userManager.Setup(m => m.FindByNameAsync("a@b.com")).ReturnsAsync(user);
        _signInManager.Setup(s => s.CheckPasswordSignInAsync(user, "pw", false)).ReturnsAsync(SignInResult.Success);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Forwarded-For"] = "203.0.113.9, 10.0.0.1";
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(ctx);
        RefreshTokenModel? captured = null;
        _repo.Setup(r => r.AddRefreshTokenAsync(It.IsAny<RefreshTokenModel>(), true))
            .Callback<RefreshTokenModel, bool>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        await _sut.Login(new AccountLoginDto { Login = "a@b.com", Password = "pw" });

        captured!.CreatedByIp.Should().Be("203.0.113.9");
    }
}
