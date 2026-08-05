using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Infrastructure.Services
{
    public class JitUserProvisioningService : IJitUserProvisioningService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<JitUserProvisioningService> _logger;

        public JitUserProvisioningService(AppDbContext context, ILogger<JitUserProvisioningService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task EnsureProvisionedAsync(ClaimsPrincipal user)
        {
            var userId = user.Identity?.Name;
            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            var alreadyProvisioned = await _context.Users.AnyAsync(u => u.Id == userId);
            if (alreadyProvisioned)
            {
                return;
            }

            var email = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value;
            var userName = user.FindFirst("preferred_username")?.Value ?? email ?? userId;

            var account = new AccountModel
            {
                Id = userId,
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email?.ToUpperInvariant(),
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UserSettings = new UserSettingsModel { AccountId = userId }
            };

            _context.Users.Add(account);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Another request for the same brand-new user (e.g. GetUserData and the
                // SignalR handshake firing back-to-back right after login) already won
                // the race between the AnyAsync check above and this SaveChanges.
                _logger.LogDebug(ex, "JIT provisioning race for user {UserId} — already provisioned.", userId);
            }
        }
    }
}
