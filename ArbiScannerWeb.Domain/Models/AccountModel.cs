using System;

namespace ArbiScannerWeb.Domain.Models
{
    // Not backed by ASP.NET Core Identity — auth is delegated to Keycloak, and
    // this row is JIT-provisioned on first authenticated request (see
    // ArbiScannerWeb.Infrastructure/Services/JitUserProvisioningService.cs).
    //
    // Class name, the Users DbSet, and Id/UserName/NormalizedUserName/Email/
    // NormalizedEmail must NOT change: ArbiScannerAdminPannel's
    // WebAppUserRepository reads this type directly by name/shape from the
    // sibling submodule.
    public class AccountModel
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? NormalizedUserName { get; set; }
        public string? Email { get; set; }
        public string? NormalizedEmail { get; set; }
        public bool EmailConfirmed { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int UserSettingsId { get; set; }
        public UserSettingsModel UserSettings { get; set; } = new();
    }
}
