using ArbiScannerWeb.Abstractions.Messaging;
using ArbiScannerWeb.Infrastructure.Messaging;
using System.Collections.Concurrent;
using System.Text;
using ArbiScannerWeb.Messaging.Kafka;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ArbiScannerWeb.IntegrationTests.Kafka;

/// <summary>Records every handler invocation and lets a test decide what the handler does.</summary>
internal sealed class HandlerProbe
{
    public ConcurrentQueue<ConsumedMessage> Received { get; } = new();

    public ConcurrentDictionary<string, int> Attempts { get; } = new();

    public Func<ConsumedMessage, CancellationToken, Task> Behaviour { get; set; } = (_, _) => Task.CompletedTask;

    public string Payload(ConsumedMessage message) => Encoding.UTF8.GetString(message.Value);
}

internal sealed class ProbeHandler(HandlerProbe probe) : IKafkaMessageHandler
{
    public async Task HandleAsync(ConsumedMessage message, CancellationToken cancellationToken)
    {
        probe.Received.Enqueue(message);
        probe.Attempts.AddOrUpdate(message.Metadata.MessageId ?? string.Empty, 1, (_, n) => n + 1);
        await probe.Behaviour(message, cancellationToken);
    }
}

internal sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, string> _state = new();

    public Task<ClaimResult> TryBeginAsync(string key, CancellationToken cancellationToken)
    {
        if (_state.TryAdd(key, "processing"))
        {
            return Task.FromResult(ClaimResult.Started);
        }

        return Task.FromResult(_state.TryGetValue(key, out var s) && s == "done" ? ClaimResult.AlreadyDone : ClaimResult.InProgress);
    }

    public Task CompleteAsync(string key)
    {
        _state[key] = "done";
        return Task.CompletedTask;
    }

    public Task AbandonAsync(string key)
    {
        _state.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}

/// <summary>A real generic host running the production <see cref="KafkaConsumerService"/>.</summary>
internal sealed class ConsumerHost : IAsyncDisposable
{
    private readonly IHost _host;

    private ConsumerHost(IHost host, HandlerProbe probe)
    {
        _host = host;
        Probe = probe;
    }

    public HandlerProbe Probe { get; }

    public static async Task<ConsumerHost> StartAsync(
        string bootstrapServers,
        string topic,
        string groupId,
        Action<HandlerProbe>? configureProbe = null,
        int maxHandlerAttempts = 2)
    {
        var probe = new HandlerProbe();
        configureProbe?.Invoke(probe);

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kafka:BootstrapServers"] = bootstrapServers,
            ["Kafka:Consumer:GroupId"] = groupId,
            ["Kafka:Consumer:Topic"] = topic,
            ["Kafka:Consumer:AutoOffsetReset"] = "Earliest",
            ["Kafka:Consumer:CommitIntervalSeconds"] = "1",
            ["Kafka:Consumer:PollTimeoutMs"] = "200",
            ["Kafka:Consumer:SessionTimeoutMs"] = "10000",
            ["Kafka:Consumer:MaxHandlerAttempts"] = maxHandlerAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Kafka:Consumer:RetryBaseDelayMs"] = "20",
            ["Kafka:Consumer:RetryMaxDelayMs"] = "50",
        });
        builder.Services.AddSingleton(probe);
        builder.Services.AddKafkaMessaging(builder.Configuration);
        builder.Services.AddKafkaConsumer<ProbeHandler>(builder.Configuration);

        var host = builder.Build();
        await host.StartAsync();
        return new ConsumerHost(host, probe);
    }

    /// <summary>Graceful shutdown: commits what was processed and leaves the group.</summary>
    public Task StopAsync() => _host.StopAsync();

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _host.StopAsync();
        }
        finally
        {
            _host.Dispose();
        }
    }
}

internal static class KafkaTestSupport
{
    public static KafkaMessageProducer CreateProducer(string bootstrapServers) =>
        new(Options.Create(new KafkaOptions { BootstrapServers = bootstrapServers, LingerMs = 0 }), NullLogger<KafkaMessageProducer>.Instance);

    public static Task<ProducedMessage> SendAsync(IKafkaMessageProducer producer, string topic, string key, string payload, string? messageId = null) =>
        producer.PublishAsync(topic, key, Encoding.UTF8.GetBytes(payload), "TradeOpportunity", messageId ?? $"{key}/{payload}");

    public static async Task UntilAsync(Func<bool> condition, string because, int timeoutSeconds = 90)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out after {timeoutSeconds}s waiting for: {because}");
            }

            await Task.Delay(100);
        }
    }

    /// <summary>Reads every message currently on <paramref name="topic"/> from the beginning.</summary>
    public static List<ConsumeResult<string, byte[]>> ReadAll(string bootstrapServers, string topic, int expected, int timeoutSeconds = 30)
    {
        using var consumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "reader-" + Guid.NewGuid().ToString("N"),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).Build();
        consumer.Subscribe(topic);

        var results = new List<ConsumeResult<string, byte[]>>();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (results.Count < expected && DateTime.UtcNow < deadline)
        {
            var result = consumer.Consume(TimeSpan.FromMilliseconds(500));
            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results;
    }
}
