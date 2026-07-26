using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class AccountServiceTests
{
    private readonly Mock<UserManager<AccountModel>> _userManager;
    private readonly Mock<SignInManager<AccountModel>> _signInManager;
    private readonly Mock<IAccountRepository> _repo;
    private readonly Mock<IEmailService> _email;
    private readonly AccountService _sut;

    public AccountServiceTests()
    {
        _userManager = MockHelpers.CreateUserManagerMock();
        _signInManager = MockHelpers.CreateSignInManagerMock(_userManager);
        var (redis, _) = MockHelpers.CreateRedisMocks();
        _repo = new Mock<IAccountRepository>();
        _email = new Mock<IEmailService>();

        var config = new Mock<IConfiguration>();
        config.Setup(c => c["ClientUrl"]).Returns("http://localhost:3001");

        _sut = new AccountService(
            _signInManager.Object,
            _userManager.Object,
            redis.Object,
            new Mock<IHttpContextAccessor>().Object,
            MockHelpers.CreateInMemoryDbContext(),
            _repo.Object,
            _email.Object,
            Options.Create(MockHelpers.CreateTestJwtOptions()),
            config.Object,
            NullLogger<AccountService>.Instance);
    }

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
}
