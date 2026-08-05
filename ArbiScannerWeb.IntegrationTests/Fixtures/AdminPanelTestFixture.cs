using ArbiScannerWeb.IntegrationTests.Support;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ArbiScannerWeb.IntegrationTests.Fixtures;

public sealed class AdminPanelTestFixture : IAsyncLifetime
{
    public const string AdminServiceClientId = "integration-test-admin-service";
    public const string AdminServiceClientSecret = "IntegrationTest@123";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(Images.Postgres)
        .WithDatabase("ArbiScannerBot")
        .WithUsername("postgres")
        .WithPassword("REDACTED")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder(Images.Redis)
        .Build();

    internal WireMockServer AdminPanelApi { get; private set; } = default!;
    internal CustomWebApplicationFactory Factory { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        // One WireMock instance stands in for both the real AdminPanel API and the
        // Keycloak token endpoint AdminService now authenticates against (Client
        // Credentials grant, see keycloak/realm-export/arbiscanner-admin-realm.json)
        // - the two are distinguished only by request path.
        AdminPanelApi = WireMockServer.Start();

        AdminPanelApi
            .Given(Request.Create().WithPath("/protocol/openid-connect/token").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { access_token = "stub-admin-service-token", expires_in = 300, token_type = "Bearer" }));

        Factory = new CustomWebApplicationFactory(
            new Dictionary<string, string?>(JwtTestSettings.ConfigOverrides)
            {
                ["ConnectionStrings:SqlServer"] = _postgres.GetConnectionString(),
                ["Redis:Endpoint"] = _redis.GetConnectionString(),
                ["AdminApiUrl"] = AdminPanelApi.Url,
                ["Keycloak:AdminService:Authority"] = AdminPanelApi.Url,
                ["Keycloak:AdminService:ClientId"] = AdminServiceClientId,
                ["Keycloak:AdminService:ClientSecret"] = AdminServiceClientSecret,
                ["RabbitMq:Host"] = "127.0.0.1",
                ["RabbitMq:Port"] = "1",
                ["RabbitMq:Queue"] = "spread_api_unused",
                ["RabbitMq:Exchange"] = "spread_fanout_exchange_unused",
                ["RabbitMq:RoutingKey"] = "",
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest",
                ["MongoDb:ConnectionString"] = "mongodb://127.0.0.1:1/",
                ["MongoDb:DatabaseName"] = "unused",
            },
            configureTestServices: JwtTestSettings.ConfigureTestJwtBearer);

        _ = Factory.Services;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        AdminPanelApi.Stop();
        AdminPanelApi.Dispose();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }
}
