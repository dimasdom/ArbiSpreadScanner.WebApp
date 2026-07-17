using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.DbContext;
using ArbiScannerWeb.Infrastructure.Repositories;
using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Infrastructure
{
    public static class StartupSetup
    {
        private const string AccessTokenCookieName = "arbiscanner.access_token";

        public static void AddDbContext(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<AppDbContext>(options =>
              options.UseNpgsql(connectionString),
              optionsLifetime: ServiceLifetime.Singleton);
            services.AddDbContextFactory<AppDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
            });
        }

        public static void AddMongoDb(this IServiceCollection services, IConfiguration configuration)
        {
            var settings = configuration.GetSection(MongoDbSettings.SectionName).Get<MongoDbSettings>()
                ?? throw new InvalidOperationException("MongoDB options not configured");

            BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

            if (!BsonClassMap.IsClassMapRegistered(typeof(TradeOpportunityTickerModel)))
            {
                BsonClassMap.RegisterClassMap<TradeOpportunityTickerModel>(cm =>
                {
                    cm.AutoMap();
                    cm.UnmapProperty(x => x.Id);
                    cm.SetIdMember(null);
                    cm.SetIgnoreExtraElements(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(ExchangeRateModel)))
            {
                BsonClassMap.RegisterClassMap<ExchangeRateModel>(cm =>
                {
                    cm.AutoMap();
                    cm.SetIgnoreExtraElements(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(TradeOpportunityModel)))
            {
                BsonClassMap.RegisterClassMap<TradeOpportunityModel>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdProperty(x => x.Guid);
                    cm.SetIgnoreExtraElements(true);
                });
            }

            services.AddSingleton(settings);
            services.AddSingleton<IMongoClient>(_ => new MongoClient(settings.ConnectionString));
            services.AddSingleton<IMongoDatabase>(sp =>
                sp.GetRequiredService<IMongoClient>().GetDatabase(settings.DatabaseName));
        }

        public static void AddServices(this IServiceCollection services) =>
            services.AddScoped<IAccountService, AccountService>()
            .AddScoped<IAccountRepository, AccountRepository>()
            .AddScoped<ITradeOpportunityService, ArbiScannerWeb.Infrastructure.Services.TradeOpportunityService>()
            .AddSingleton<ITradeOpportunityRepository, TradeOpportunityRepositoryMongo>()
            .AddSingleton<ITradeOpportunityTickerRepository, TradeOpportunityTickerRepositoryMongo>()
            .AddScoped<IEmailService,EmailService>()
            .AddSingleton<IAdminService, AdminService>()
            .AddScoped<IUserSettingsService, UserSettingsService>()
            .AddScoped<ISubscriptionService, SubscriptionService>()
            .AddScoped<IExchangeLinkRepository, ExchangeLinkRepository>();

        public static void AddIdentity(this IServiceCollection services) =>
            services.AddIdentityCore<AccountModel>(config =>
            {
                config.Password.RequireNonAlphanumeric = true;
                config.Password.RequiredLength = 12;
                config.Password.RequireUppercase = true;
                config.SignIn.RequireConfirmedPhoneNumber = false;
                config.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager<SignInManager<AccountModel>>()
            .AddUserManager<UserManager<AccountModel>>()
            .AddDefaultTokenProviders();
        public static void AddJwtOptions(this IServiceCollection services, IConfiguration configuration) =>
            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        public static void AddAuthenticationJwt(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() 
                ?? throw new InvalidOperationException("JWT options not configured");

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtOptions.SigningKey)),
                    ValidateIssuerSigningKey = true,
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrWhiteSpace(context.Token) && context.Request.Cookies.TryGetValue(AccessTokenCookieName, out var token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    }
                };
            });
        }

        public static async Task SeedExchangesAsync(this IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await context.Database.EnsureCreatedAsync();

            var requiredExchanges = new[]
            {
                "Binance", "Bybit", "OKX", "KuCoin Futures", "MEXC", "Bitget",
                "HTX", "XT", "CoinEX", "LBank", "WhiteBit", "Gate.io", "BingX"
            };

            var existingNames = await context.Exchanges
                .Select(e => e.Name)
                .ToListAsync();

            var missing = requiredExchanges
                .Where(name => !existingNames.Contains(name))
                .Select(name => new ExchangeModel { Name = name })
                .ToList();

            if (missing.Count > 0)
            {
                context.Exchanges.AddRange(missing);
                await context.SaveChangesAsync();
            }

            await SeedExchangeLinksAsync(context);
        }

        private static async Task SeedExchangeLinksAsync(AppDbContext context)
        {
            var links = new[]
            {
                new ExchangeLinkModel { Exchange = "Binance",       Spot = "https://www.binance.com/en/trade/{base}_{quote}",           Futures = "https://www.binance.com/en/futures/{base}{quote}",                                                                                         Funding = "https://www.binance.com/en/futures/{base}{quote}" },
                new ExchangeLinkModel { Exchange = "Bybit",         Spot = "https://www.bybit.com/en/trade/spot/{base}/{quote}",         Futures = "https://www.bybit.com/en/trade/futures/linear/{base}{quote}",                                                                             Funding = "https://www.bybit.com/en/trade/futures/linear/{base}{quote}" },
                new ExchangeLinkModel { Exchange = "MEXC",          Spot = "https://www.mexc.com/en-US/exchange/{base}_{quote}",         Futures = "https://futures.mexc.com/exchange/{base}_{quote}",                                                                                        Funding = "https://futures.mexc.com/exchange/{base}_{quote}" },
                new ExchangeLinkModel { Exchange = "OKX",           Spot = "https://www.okx.com/trade-spot/{base}-{quote}",             Futures = "https://www.okx.com/trade-swap/{base}-{quote}-SWAP",                                                                                      Funding = "https://www.okx.com/trade-swap/{base}-{quote}-SWAP" },
                new ExchangeLinkModel { Exchange = "HTX",           Spot = "https://www.htx.com/trade/{base}_{quote}?type=spot",        Futures = "https://www.htx.com/futures/linear_swap/exchange#contract_code={base}-{quote}&contract_type=swap&type=cross",                             Funding = "https://www.htx.com/futures/linear_swap/exchange#contract_code={base}-{quote}&contract_type=swap&type=cross" },
                new ExchangeLinkModel { Exchange = "CoinEX",        Spot = "https://www.coinex.com/en/exchange/{base}-{quote}",         Futures = "https://www.coinex.com/en/futures/{base}-{quote}",                                                                                        Funding = "https://www.coinex.com/en/futures/{base}-{quote}" },
                new ExchangeLinkModel { Exchange = "KuCoinFutures", Spot = "https://www.kucoin.com/trade/{base}-{quote}",               Futures = "https://www.kucoin.com/futures/trade/{base}{quote}M",                                                                                     Funding = "https://www.kucoin.com/futures/trade/{base}{quote}M" },
                new ExchangeLinkModel { Exchange = "KuCoin Futures", Spot = "https://www.kucoin.com/trade/{base}-{quote}",               Futures = "https://www.kucoin.com/futures/trade/{base}{quote}M",                                                                                     Funding = "https://www.kucoin.com/futures/trade/{base}{quote}M" },
                new ExchangeLinkModel { Exchange = "BingX",         Spot = "https://bingx.com/en/spot/{base}{quote}",                   Futures = "https://bingx.com/en/perpetual/{base}-{quote}",                                                                                           Funding = "https://bingx.com/en/perpetual/{base}-{quote}" },
                new ExchangeLinkModel { Exchange = "Gateio",        Spot = "https://www.gate.io/en/trade/{base}_{quote}",               Futures = "https://www.gate.io/en/futures/USDT/{base}_{quote}",                                                                                      Funding = "https://www.gate.io/en/futures/USDT/{base}_{quote}" },
                new ExchangeLinkModel { Exchange = "XT",            Spot = "https://www.xt.com/en/trade/{base}_{quote}",                Futures = "https://www.xt.com/en/futures/trade/{base}_{quote}",                                                                                      Funding = "https://www.xt.com/en/futures/trade/{base}_{quote}" },
                new ExchangeLinkModel { Exchange = "LBank",         Spot = "https://www.lbank.com/en-US/trade/{base}_{quote}",          Futures = "https://www.lbank.com/en-US/futures/{base}{quote}",                                                                                       Funding = "https://www.lbank.com/en-US/futures/{base}{quote}" },
                new ExchangeLinkModel { Exchange = "WhiteBit",      Spot = "https://whitebit.com/trade/{base}-{quote}",                 Futures = "https://whitebit.com/trade/{base}-PERP",                                                                                                  Funding = "https://whitebit.com/trade/{base}-PERP" },
            };

            var existingExchanges = await context.ExchangeLinks
                .Select(e => e.Exchange)
                .ToListAsync();

            var missingLinks = links
                .Where(l => !existingExchanges.Contains(l.Exchange))
                .ToList();

            if (missingLinks.Count > 0)
            {
                context.ExchangeLinks.AddRange(missingLinks);
                await context.SaveChangesAsync();
            }
        }
    }
}

