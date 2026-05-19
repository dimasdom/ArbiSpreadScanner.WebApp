using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.EntityConfigurations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Infrastructure.DbContext
{
    public class AppDbContext : IdentityDbContext<AccountModel>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }
        public DbSet<UserSettingsModel> UsersSettings { get; set; }
        public DbSet<RefreshTokenModel> RefreshTokens { get; set; }
        public DbSet<TradeOpportunityModel> CurrentSpreads { get; set; }
        public DbSet<TradeOpportunityTickerModel> SpreadsTicker { get; set; }
        public DbSet<ExchangeRateModel> ExchangeRates { get; set; }
        public DbSet<EmailConfirmationCodes> EmailConfirmationCodes { get; set; }
        public DbSet<ForgetPasswordRequest> ForgetPasswordRequests { get; set; }
        public DbSet<TelegramLinkRequest> TelegramLinkRequests { get; set; }
        public DbSet<ExchangeModel> Exchanges { get; set; }
        public DbSet<UserExchangeModel> UserExchanges { get; set; }
        public DbSet<ExchangeLinkModel> ExchangeLinks { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ApplyConfiguration(new TradeOpportunityModelConfiguration());
            builder.ApplyConfiguration(new TradeOpportunityTickerModelConfiguration());

            builder.Entity<UserSettingsModel>()
               .ToTable("UserSettings");

            builder.Entity<AccountModel>()
                .HasOne(a => a.UserSettings)
                .WithOne()
                .HasForeignKey<AccountModel>(a => a.UserSettingsId);

            builder.Entity<TradeOpportunityModel>()
                .ToTable("CurrentSpreads");

            builder.Entity<TradeOpportunityModel>()
                .HasKey(p => p.Guid);

            builder.Entity<TradeOpportunityTickerModel>()
                .ToTable("SpreadsTicker");

            builder.Entity<ExchangeRateModel>()
           .ToTable("ExchangeRates");

            builder.Entity<TradeOpportunityModel>()
                .HasOne(p => p.ExchangeRateA)
                .WithMany()
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TradeOpportunityModel>()
                .HasOne(p => p.ExchangeRateB)
                .WithMany()
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TradeOpportunityModel>()
                .HasOne(p => p.ExchangeShort)
                .WithMany()
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TradeOpportunityModel>()
                .HasOne(p => p.ExchangeLong)
                .WithMany()
                .OnDelete(DeleteBehavior.Restrict);

            // Configure RefreshTokenModel
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
