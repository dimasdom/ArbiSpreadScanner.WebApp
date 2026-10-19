using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.IntegrationTests.Support;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.Kafka;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace ArbiScannerWeb.IntegrationTests.Fixtures;

/// <summary>The whole web API with Kafka as its only spread source (<c>Messaging:Consumer:Mode=Kafka</c>) against real
/// Postgres, Redis, Mongo and Kafka. No RabbitMQ is involved anywhere.</summary>
public sealed class KafkaWebTestFixture : IAsyncLifetime
{
    public const string Topic = "spreads";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(Images.Postgres)
        .WithDatabase("ArbiScannerBot")
        .WithUsername("postgres")
        .WithPassword("REDACTED")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder(Images.Redis).Build();
    private readonly MongoDbContainer _mongo = new MongoDbBuilder(Images.Mongo).Build();
    private readonly KafkaContainer _kafka = new KafkaBuilder(Images.Kafka).Build();

    private readonly List<string> _environmentVariables = [];

    internal CustomWebApplicationFactory Factory { get; private set; } = default!;

    public string BootstrapServers => _kafka.GetBootstrapAddress();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _mongo.StartAsync(), _kafka.StartAsync());

        using (var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = BootstrapServers }).Build())
        {
            await admin.CreateTopicsAsync(
            [
                new TopicSpecification { Name = Topic, NumPartitions = 3, ReplicationFactor = 1 },
                new TopicSpecification { Name = Topic + ".dlq", NumPartitions = 1, ReplicationFactor = 1 },
            ]);
        }

        var overrides = new Dictionary<string, string?>(JwtTestSettings.ConfigOverrides)
            {
                ["ConnectionStrings:SqlServer"] = _postgres.GetConnectionString(),
                ["Redis:Endpoint"] = _redis.GetConnectionString(),
                ["MongoDb:ConnectionString"] = _mongo.GetConnectionString(),
                ["MongoDb:DatabaseName"] = "ArbiScannerWebKafkaTests",
                ["MongoDb:CurrentSpreadsCollection"] = "CurrentSpreads",
                ["MongoDb:SpreadsTickerCollection"] = "SpreadsTicker",
                ["Messaging:ServiceName"] = "webapp",
                ["Messaging:Consumer:Mode"] = "Kafka",
                ["Kafka:BootstrapServers"] = BootstrapServers,
                ["Kafka:Consumer:Topic"] = Topic,
                ["Kafka:Consumer:AutoOffsetReset"] = "Earliest",
                ["Kafka:Consumer:CommitIntervalSeconds"] = "1",
                ["Kafka:Consumer:PollTimeoutMs"] = "200",
                ["Kafka:Consumer:RetryBaseDelayMs"] = "50",
                ["Kafka:Consumer:RetryMaxDelayMs"] = "100",
            };

        // Program.cs reads the messaging mode while it builds the service collection, which is before WebApplicationFactory's
        // in-memory overrides are visible. Environment variables are, exactly as they are for the deployed container.
        foreach (var (key, value) in overrides.Where(o => o.Key.StartsWith("Messaging:", StringComparison.Ordinal) || o.Key.StartsWith("Kafka:", StringComparison.Ordinal)))
        {
            var name = key.Replace(":", "__", StringComparison.Ordinal);
            Environment.SetEnvironmentVariable(name, value);
            _environmentVariables.Add(name);
        }

        Factory = new CustomWebApplicationFactory(
            overrides,
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
        foreach (var name in _environmentVariables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        await Factory.DisposeAsync();
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask(),
            _mongo.DisposeAsync().AsTask(),
            _kafka.DisposeAsync().AsTask());
    }
}

[CollectionDefinition(Name)]
public sealed class KafkaWebCollection : ICollectionFixture<KafkaWebTestFixture>
{
    public const string Name = "Kafka web integration tests";
}
