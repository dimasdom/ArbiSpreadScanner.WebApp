using ArbiScannerWeb.IntegrationTests.Support;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Testcontainers.Kafka;

namespace ArbiScannerWeb.IntegrationTests.Kafka;

public sealed class KafkaTestFixture : IAsyncLifetime
{
    private readonly KafkaContainer _kafka = new KafkaBuilder(Images.Kafka).Build();

    public string BootstrapServers => _kafka.GetBootstrapAddress();

    public Task InitializeAsync() => _kafka.StartAsync();

    public Task DisposeAsync() => _kafka.DisposeAsync().AsTask();

    /// <summary>Creates a uniquely named topic (and its dead-letter twin) so tests never see each other's data.</summary>
    public async Task<string> CreateTopicAsync(int partitions)
    {
        var topic = "spreads-" + Guid.NewGuid().ToString("N")[..12];

        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = BootstrapServers }).Build();
        await admin.CreateTopicsAsync(
        [
            new TopicSpecification { Name = topic, NumPartitions = partitions, ReplicationFactor = 1 },
            new TopicSpecification { Name = topic + ".dlq", NumPartitions = 1, ReplicationFactor = 1 },
        ]);

        return topic;
    }
}

[CollectionDefinition(Name)]
public sealed class KafkaCollection : ICollectionFixture<KafkaTestFixture>
{
    public const string Name = "Kafka integration tests";
}
