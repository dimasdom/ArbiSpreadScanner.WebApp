using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Infrastructure;
using ArbiScannerWeb.Infrastructure.DbContext;
using ArbiScannerWeb.Infrastructure.Services;
using ArbiScannerWeb.Infrastructure.Settings;
using ArbiScannerWeb.API.Filters;
using ArbiScannerWeb.API.Hubs;
using ArbiScannerWeb.API.Middleware;
using ArbiScannerWeb.API.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SpaServices.Extensions;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration));

// Add services to the container.

builder.Services.AddControllers(opts => opts.Filters.Add<ResultFailLoggingFilter>());
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddDbContext(builder.Configuration.GetConnectionString("SqlServer")!);
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
// Register custom services
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();
builder.Services.AddScoped<IRealtimeNotifier, SignalRService>();
builder.Services.AddHostedService<MessageProcessingService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisEndpoint = builder.Configuration["Redis:Endpoint"] ?? "localhost:6379";
    return ConnectionMultiplexer.ConnectAsync(redisEndpoint).GetAwaiter().GetResult();
});
builder.Services.AddHttpClient("AdminApi", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var adminApiUrl = config["AdminApiUrl"]
        ?? throw new InvalidOperationException("AdminApiUrl configuration is required");
    client.BaseAddress = new Uri(adminApiUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? ["https://localhost:12321"];

    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
             .AllowAnyHeader()
            .AllowAnyMethod()
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

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();
app.UseCors("AllowAll");
app.MapHub<TradeOpportunityHub>("/hubs/TradeOpportunities").RequireCors("AllowAll");
app.UseDefaultFiles();
app.MapStaticAssets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireLocalRequestFilter()],
    DashboardTitle = "ArbiScanner Jobs"
});

app.MapControllers();

await app.RunAsync();
