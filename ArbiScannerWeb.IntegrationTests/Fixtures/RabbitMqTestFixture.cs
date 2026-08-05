using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace ArbiScannerWeb.IntegrationTests.Fixtures;

public sealed class RabbitMqTestFixture : IAsyncLifetime
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

    public const string BrokerUsername = "integration-test";
    public const string BrokerPassword = "integration-test";

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder(Images.RabbitMq)
        .WithUsername(BrokerUsername)
        .WithPassword(BrokerPassword)
        .Build();

    public const string Exchange = "spread_fanout_exchange";
    public const string Queue = "spread_api";

    internal CustomWebApplicationFactory Factory { get; private set; } = default!;

    internal (string Host, int Port, string Username, string Password) BrokerEndpoint { get; private set; }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _mongo.StartAsync(), _rabbitMq.StartAsync());

        BrokerEndpoint = (_rabbitMq.Hostname, _rabbitMq.GetMappedPublicPort(5672), BrokerUsername, BrokerPassword);

        Factory = new CustomWebApplicationFactory(
            new Dictionary<string, string?>(JwtTestSettings.ConfigOverrides)
            {
                ["ConnectionStrings:SqlServer"] = _postgres.GetConnectionString(),
                ["Redis:Endpoint"] = _redis.GetConnectionString(),
                ["MongoDb:ConnectionString"] = _mongo.GetConnectionString(),
                ["MongoDb:DatabaseName"] = "ArbiScannerWebIntegrationTests",
                ["MongoDb:CurrentSpreadsCollection"] = "CurrentSpreads",
                ["MongoDb:SpreadsTickerCollection"] = "SpreadsTicker",
                ["RabbitMq:Host"] = BrokerEndpoint.Host,
                ["RabbitMq:Port"] = BrokerEndpoint.Port.ToString(),
                ["RabbitMq:Queue"] = Queue,
                ["RabbitMq:Exchange"] = Exchange,
                ["RabbitMq:RoutingKey"] = "",
                ["RabbitMq:Username"] = BrokerEndpoint.Username,
                ["RabbitMq:Password"] = BrokerEndpoint.Password,
            },
            configureTestServices: services =>
            {
                JwtTestSettings.ConfigureTestJwtBearer(services);

                services.RemoveAll<ISubscriptionService>();
                services.AddScoped<ISubscriptionService, FakeSubscriptionService>();
            });

        _ = Factory.Services;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask(),
            _mongo.DisposeAsync().AsTask(),
            _rabbitMq.DisposeAsync().AsTask());
    }
}
