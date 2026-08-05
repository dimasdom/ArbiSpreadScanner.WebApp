using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure.DbContext;
using ArbiScannerWeb.Infrastructure.Repositories;
using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Infrastructure
{
    public static class StartupSetup
    {
        private static readonly object MongoClassMapLock = new();

        public static void AddDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
              options.UseNpgsql(configuration.GetConnectionString("SqlServer"), o => o.EnableRetryOnFailure()),
              optionsLifetime: ServiceLifetime.Singleton);
            services.AddDbContextFactory<AppDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("SqlServer"), o => o.EnableRetryOnFailure());
            });
        }

        public static void AddMongoDb(this IServiceCollection services, IConfiguration configuration)
        {
            BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

            lock (MongoClassMapLock)
            {
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
            }

            services.AddSingleton(sp => configuration.GetSection(MongoDbSettings.SectionName).Get<MongoDbSettings>()
                ?? throw new InvalidOperationException("MongoDB options not configured"));
            services.AddSingleton<IMongoClient>(sp => new MongoClient(sp.GetRequiredService<MongoDbSettings>().ConnectionString));
            services.AddSingleton<IMongoDatabase>(sp =>
                sp.GetRequiredService<IMongoClient>().GetDatabase(sp.GetRequiredService<MongoDbSettings>().DatabaseName));
        }

        public static void AddServices(this IServiceCollection services) =>
            services.AddScoped<IAccountService, AccountService>()
            .AddScoped<IJitUserProvisioningService, JitUserProvisioningService>()
            .AddScoped<ITradeOpportunityService, ArbiScannerWeb.Infrastructure.Services.TradeOpportunityService>()
            .AddSingleton<ITradeOpportunityRepository, TradeOpportunityRepositoryMongo>()
            .AddSingleton<ITradeOpportunityTickerRepository, TradeOpportunityTickerRepositoryMongo>()
            .AddSingleton<IAdminService, AdminService>()
            .AddScoped<IUserSettingsService, UserSettingsService>()
            .AddScoped<ISubscriptionService, SubscriptionService>()
            .AddScoped<IExchangeLinkRepository, ExchangeLinkRepository>();

        public static void AddJwtOptions(this IServiceCollection services, IConfiguration configuration) =>
            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // Pure resource-server config: no local Identity store or signing key —
        // Keycloak (arbiscanner-web realm) issues tokens, this just validates them
        // against its published JWKS via Authority-based discovery.
        public static void AddAuthenticationJwt(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                    ?? throw new InvalidOperationException("JWT options not configured");

                options.Authority = jwtOptions.Authority;
                options.Audience = jwtOptions.Audience;
                // Only relaxed for local dev against a plain-HTTP Keycloak container
                // (see docker-compose.local.yml) — every real deployment keeps this true.
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                // Without this, ASP.NET remaps short JWT claim names (e.g. "sub") to
                // long legacy URIs before TokenValidationParameters ever sees them, so
                // NameClaimType = "sub" below would silently never match anything.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Keycloak's user id (sub) is what every User.Identity?.Name call
                    // site in this codebase expects.
                    NameClaimType = "sub",
                };
                options.Events = new JwtBearerEvents
                {
                    // Browsers can't set an Authorization header on a WebSocket
                    // handshake — the SignalR client instead appends the token as an
                    // access_token query param (see signalrService.ts's
                    // accessTokenFactory), which only the hub's own paths should honor.
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
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

        private const string KuCoinFuturesUrl = "https://www.kucoin.com/futures/trade/{base}{quote}M";

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
                new ExchangeLinkModel { Exchange = "KuCoinFutures", Spot = "https://www.kucoin.com/trade/{base}-{quote}",               Futures = KuCoinFuturesUrl,                                                                                     Funding = KuCoinFuturesUrl },
                new ExchangeLinkModel { Exchange = "KuCoin Futures", Spot = "https://www.kucoin.com/trade/{base}-{quote}",               Futures = KuCoinFuturesUrl,                                                                                     Funding = KuCoinFuturesUrl },
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

