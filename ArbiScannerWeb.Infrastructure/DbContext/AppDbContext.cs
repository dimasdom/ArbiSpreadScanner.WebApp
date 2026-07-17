using ArbiScannerWeb.Domain.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ArbiScannerWeb.Infrastructure.DbContext
{
    public class AppDbContext : IdentityDbContext<AccountModel>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }
        public DbSet<UserSettingsModel> UsersSettings { get; set; }
        public DbSet<RefreshTokenModel> RefreshTokens { get; set; }
        public DbSet<EmailConfirmationCodes> EmailConfirmationCodes { get; set; }
        public DbSet<ForgetPasswordRequest> ForgetPasswordRequests { get; set; }
        public DbSet<TelegramLinkRequest> TelegramLinkRequests { get; set; }
        public DbSet<ExchangeModel> Exchanges { get; set; }
        public DbSet<UserExchangeModel> UserExchanges { get; set; }
        public DbSet<ExchangeLinkModel> ExchangeLinks { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
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

            builder.Entity<RefreshTokenModel>()
                .ToTable("RefreshTokens");

            builder.Entity<RefreshTokenModel>()
                .HasKey(rt => rt.Id);

            builder.Entity<RefreshTokenModel>()
                .HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RefreshTokenModel>()
                .HasOne(rt => rt.ReplacedByToken)
                .WithMany()
                .HasForeignKey(rt => rt.ReplacedByTokenId)
                .OnDelete(DeleteBehavior.SetNull);

            base.OnModelCreating(builder);
        }
    }
}
