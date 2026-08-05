using System.Security.Claims;
using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArbiScannerWeb.Tests.Infrastructure;

public class JitUserProvisioningServiceTests
{
    private static ClaimsPrincipal CreatePrincipal(string sub, string? email = null, string? preferredUsername = null)
    {
        var claims = new List<Claim> { new(ClaimsIdentity.DefaultNameClaimType, sub) };
        if (email != null)
        {
            claims.Add(new Claim("email", email));
        }
        if (preferredUsername != null)
        {
            claims.Add(new Claim("preferred_username", preferredUsername));
        }
        var identity = new ClaimsIdentity(claims, "Bearer", ClaimsIdentity.DefaultNameClaimType, ClaimsIdentity.DefaultRoleClaimType);
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task EnsureProvisionedAsync_NewUser_CreatesAccountAndUserSettings()
    {
        var dbContext = MockHelpers.CreateInMemoryDbContext();
        var sut = new JitUserProvisioningService(dbContext, NullLogger<JitUserProvisioningService>.Instance);
        var principal = CreatePrincipal("keycloak-sub-1", email: "a@b.com", preferredUsername: "alice");

        await sut.EnsureProvisionedAsync(principal);

        var account = dbContext.Users.Single(u => u.Id == "keycloak-sub-1");
        account.Email.Should().Be("a@b.com");
        account.UserName.Should().Be("alice");
        account.EmailConfirmed.Should().BeTrue();
        dbContext.UsersSettings.Should().ContainSingle(s => s.AccountId == "keycloak-sub-1");
    }

    [Fact]
    public async Task EnsureProvisionedAsync_ExistingUser_DoesNotDuplicate()
    {
        var dbContext = MockHelpers.CreateInMemoryDbContext();
        var sut = new JitUserProvisioningService(dbContext, NullLogger<JitUserProvisioningService>.Instance);
        var principal = CreatePrincipal("keycloak-sub-2", email: "b@c.com");

        await sut.EnsureProvisionedAsync(principal);
        await sut.EnsureProvisionedAsync(principal);

        dbContext.Users.Count(u => u.Id == "keycloak-sub-2").Should().Be(1);
    }

    [Fact]
    public async Task EnsureProvisionedAsync_NoSubClaim_DoesNothing()
    {
        var dbContext = MockHelpers.CreateInMemoryDbContext();
        var sut = new JitUserProvisioningService(dbContext, NullLogger<JitUserProvisioningService>.Instance);
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        await sut.EnsureProvisionedAsync(principal);

        dbContext.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task EnsureProvisionedAsync_NoEmailOrPreferredUsername_FallsBackToSubAsUserName()
    {
        var dbContext = MockHelpers.CreateInMemoryDbContext();
        var sut = new JitUserProvisioningService(dbContext, NullLogger<JitUserProvisioningService>.Instance);
        var principal = CreatePrincipal("keycloak-sub-3");

        await sut.EnsureProvisionedAsync(principal);

        var account = dbContext.Users.Single(u => u.Id == "keycloak-sub-3");
        account.UserName.Should().Be("keycloak-sub-3");
        account.Email.Should().BeNull();
    }
}
