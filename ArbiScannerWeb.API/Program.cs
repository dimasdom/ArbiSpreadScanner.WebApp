using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure;
using ArbiScannerWeb.Infrastructure.DbContext;
using ArbiScannerWeb.Infrastructure.HealthChecks;
using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Infrastructure.Settings;
using ArbiScannerWeb.Infrastructure.Filters;
using ArbiScannerWeb.API.Filters;
using ArbiScannerWeb.API.Hubs;
using ArbiScannerWeb.Infrastructure.Middleware;
using ArbiScannerWeb.API.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Enrichers.Span;
using StackExchange.Redis;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .Enrich.WithSpan();

        // VerboseLogging:Enabled (VERBOSE_CONSOLE_LOGS in .env) raises the minimum
        // level to Debug when console output is easier to check than Grafana/Loki.
        if (ctx.Configuration.GetValue("VerboseLogging:Enabled", false))
        {
            cfg.MinimumLevel.Debug();
        }
    });

    builder.Services.AddControllers(opts => opts.Filters.Add<ResultStatusCodeFilter>());
    builder.Services.AddOpenApi();
    builder.Services.AddSignalR();
    builder.Services.AddDbContext(builder.Configuration);
    builder.Services.AddIdentity();
    builder.Services.AddJwtOptions(builder.Configuration);
    builder.Services.AddAuthenticationJwt(builder.Configuration);
    builder.Services.AddMongoDb(builder.Configuration);
    builder.Services.AddServices();
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(
            builder.Configuration.GetConnectionString("SqlServer")!)));
    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = 2;
        options.Queues = ["default"];
    });
    builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
    builder.Services.AddScoped<IRealtimeNotifier, SignalRService>();
    builder.Services.AddHostedService<MessageProcessingService>();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var redisEndpoint = builder.Configuration["Redis:Endpoint"] ?? "localhost:6379";
        return ConnectionMultiplexer.ConnectAsync(redisEndpoint).GetAwaiter().GetResult();
    });
    builder.Services.AddHealthChecks()
        .AddCheck<DbContextHealthCheck<AppDbContext>>("postgres")
        .AddCheck<MongoHealthCheck>("mongo")
        .AddCheck<RedisHealthCheck>("redis")
        .AddCheck<RabbitMqHealthCheck>("rabbitmq");
    builder.Services.AddHttpClient("AdminApi", (sp, client) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var adminApiUrl = config["AdminApiUrl"]
            ?? throw new InvalidOperationException("AdminApiUrl configuration is required");
        client.BaseAddress = new Uri(adminApiUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });
    var observabilityEnabled = builder.Configuration.GetValue("Observability:Enabled", true);
    if (observabilityEnabled)
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("arbiscanner-web", serviceVersion: "1.0.0"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(o =>
                {
                    o.RecordException = true;
                    o.Filter = ctx => ctx.Request.Path != "/metrics"
                                   && !ctx.Request.Path.StartsWithSegments("/hubs");
                })
                .AddHttpClientInstrumentation(o => o.RecordException = true)
                .AddEntityFrameworkCoreInstrumentation()
                .AddRedisInstrumentation()
                .AddSource("RabbitMQ.Client.*")
                .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources")
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter());
    }
    builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"));
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetSlidingWindowLimiter(GetClientIpAddress(httpContext), _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
    });
    builder.Services.AddCors(options =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? ["https://localhost:12321"];

        options.AddPolicy("AllowAll", policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .WithMethods("GET", "POST", "DELETE")
                .WithHeaders("Content-Type")
                .AllowCredentials();
        });
    });
    var app = builder.Build();
    await app.Services.SeedExchangesAsync();

    app.Services.GetRequiredService<IRecurringJobManager>()
        .AddOrUpdate<StaleSpreadCleanupJob>(
            "stale-spread-cleanup",
            job => job.ExecuteAsync(JobCancellationToken.Null),
            "0 0 * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

    if (observabilityEnabled)
    {
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
    }
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseCors("AllowAll");
    app.MapHub<TradeOpportunityHub>("/hubs/TradeOpportunities").RequireCors("AllowAll");
    app.UseDefaultFiles();
    app.MapStaticAssets();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    if (app.Environment.IsDevelopment())
        app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireLocalRequestFilter()],
        DashboardTitle = "ArbiScanner Jobs"
    });

    app.MapControllers();
    app.MapHealthChecks("/health");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

static string GetClientIpAddress(HttpContext context)
{
    var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrEmpty(xForwardedFor))
        return xForwardedFor.Split(',')[0].Trim();

    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

public partial class Program
{
    protected Program() { }
}
