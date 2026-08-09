using ArbiScannerWeb.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace ArbiScannerWeb.Infrastructure.DbContext
{
    public class AppDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        // Not an IdentityDbContext<T> — auth is delegated to Keycloak. Rows here
        // are JIT-provisioned from validated token claims. The Users DbSet name
        // and AccountModel's shape must stay stable: ArbiScannerAdminPannel's
        // WebAppUserRepository reads this type directly from the sibling submodule.
        public DbSet<AccountModel> Users { get; set; }
        public DbSet<UserSettingsModel> UsersSettings { get; set; }
        public DbSet<TelegramLinkRequest> TelegramLinkRequests { get; set; }
        public DbSet<ExchangeModel> Exchanges { get; set; }
        public DbSet<UserExchangeModel> UserExchanges { get; set; }
        public DbSet<ExchangeLinkModel> ExchangeLinks { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<AccountModel>()
                .ToTable("Users");

            builder.Entity<UserSettingsModel>()
               .ToTable("UserSettings");

            builder.Entity<UserSettingsModel>()
                .HasIndex(x => x.AccountId)
                .HasDatabaseName("IX_UserSettings_AccountId");

            builder.Entity<UserSettingsModel>()
                .HasIndex(x => x.ChatId)
                .HasDatabaseName("IX_UserSettings_ChatId");

            builder.Entity<UserSettingsModel>()
                .HasIndex(x => x.SpreadSize, "IX_UserSettings_Active_Futures_SpreadSize")
                .HasFilter("\"Active\" = true AND \"FuturesSpread\" = true");

            builder.Entity<UserSettingsModel>()
                .HasIndex(x => x.SpreadSize, "IX_UserSettings_Active_Funding_SpreadSize")
                .HasFilter("\"Active\" = true AND \"FundingSpread\" = true");

            builder.Entity<UserSettingsModel>()
                .HasIndex(x => x.SpreadSize, "IX_UserSettings_Active_Spot_SpreadSize")
                .HasFilter("\"Active\" = true AND \"SpotSpread\" = true");

            builder.Entity<AccountModel>()
                .HasOne(a => a.UserSettings)
                .WithOne()
                .HasForeignKey<AccountModel>(a => a.UserSettingsId);

            base.OnModelCreating(builder);
        }
    }
}
