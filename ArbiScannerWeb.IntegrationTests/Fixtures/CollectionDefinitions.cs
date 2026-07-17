namespace ArbiScannerWeb.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public sealed class WebApiCollection : ICollectionFixture<WebApiTestFixture>
{
    public const string Name = "WebApi integration tests";
}

[CollectionDefinition(Name)]
public sealed class RabbitMqCollection : ICollectionFixture<RabbitMqTestFixture>
{
    public const string Name = "RabbitMq integration tests";
}

[CollectionDefinition(Name)]
public sealed class AdminPanelCollection : ICollectionFixture<AdminPanelTestFixture>
{
    public const string Name = "AdminPanel integration tests";
}
