using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace ArbiScannerWeb.IntegrationTests.Fixtures;

public sealed class WebApiTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(Images.Postgres)
        .WithDatabase("ArbiScannerBot")
        .WithUsername("postgres")
        .WithPassword("REDACTED")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder(Images.Redis)
        .Build();

    private readonly MongoDbContainer _mongo = new MongoDbBuilder(Images.Mongo)
        .Build();

    internal CustomWebApplicationFactory Factory { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _mongo.StartAsync());

        Factory = new CustomWebApplicationFactory(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:SqlServer"] = _postgres.GetConnectionString(),
                ["Redis:Endpoint"] = _redis.GetConnectionString(),
                ["MongoDb:ConnectionString"] = _mongo.GetConnectionString(),
                ["MongoDb:DatabaseName"] = "ArbiScannerWebIntegrationTests",
                ["MongoDb:CurrentSpreadsCollection"] = "CurrentSpreads",
                ["MongoDb:SpreadsTickerCollection"] = "SpreadsTicker",
                ["RabbitMq:Host"] = "127.0.0.1",
                ["RabbitMq:Port"] = "1",
                ["RabbitMq:Queue"] = "spread_api_unused",
                ["RabbitMq:Exchange"] = "spread_fanout_exchange_unused",
                ["RabbitMq:RoutingKey"] = "",
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest",
            },
            configureTestServices: services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddScoped<IEmailService, FakeEmailService>();

                services.RemoveAll<ISubscriptionService>();
                services.AddScoped<ISubscriptionService, FakeSubscriptionService>();
            });

        _ = Factory.Services;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask(), _mongo.DisposeAsync().AsTask());
    }
}
