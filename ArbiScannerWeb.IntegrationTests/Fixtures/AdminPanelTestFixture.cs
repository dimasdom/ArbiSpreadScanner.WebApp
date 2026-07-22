using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ArbiScannerWeb.IntegrationTests.Fixtures;

public sealed class AdminPanelTestFixture : IAsyncLifetime
{
    public const string AdminUserName = "integration-admin";
    public const string AdminPassword = "IntegrationTest@123";

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

        AdminPanelApi = WireMockServer.Start();

        AdminPanelApi
            .Given(Request.Create().WithPath("/api/account/Authenticate").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { isSuccess = true, value = new { token = "stub-admin-jwt" } }));

        Factory = new CustomWebApplicationFactory(
            new Dictionary<string, string?>(JwtTestSettings.ConfigOverrides)
            {
                ["ConnectionStrings:SqlServer"] = _postgres.GetConnectionString(),
                ["Redis:Endpoint"] = _redis.GetConnectionString(),
                ["AdminApiUrl"] = AdminPanelApi.Url,
                ["AdminUser:UserName"] = AdminUserName,
                ["AdminUser:Password"] = AdminPassword,
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
            configureTestServices: services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddScoped<IEmailService, FakeEmailService>();
            });

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
