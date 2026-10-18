using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArbiScannerWeb.Messaging.Kafka;

public static class KafkaServiceCollectionExtensions
{
    /// <summary>Registers the shared producer (singleton), dead-letter publisher and options.</summary>
    public static IServiceCollection AddKafkaMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BootstrapServers), "Kafka:BootstrapServers is required.")
            .ValidateOnStart();

        services.TryAddSingleton<IKafkaMessageProducer, KafkaMessageProducer>();
        services.TryAddSingleton<IDeadLetterPublisher, DeadLetterPublisher>();
        return services;
    }

    /// <summary>Adds one consumer-group member that dispatches every message to <typeparamref name="THandler"/>
    /// (scoped, resolved per message).</summary>
    public static IServiceCollection AddKafkaConsumer<THandler>(this IServiceCollection services, IConfiguration configuration)
        where THandler : class, IKafkaMessageHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<KafkaConsumerOptions>()
            .Bind(configuration.GetSection(KafkaConsumerOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.GroupId), "Kafka:Consumer:GroupId is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Topic), "Kafka:Consumer:Topic is required.")
            .Validate(o => o.MaxHandlerAttempts >= 1, "Kafka:Consumer:MaxHandlerAttempts must be at least 1.")
            .Validate(o => Enum.TryParse<Confluent.Kafka.AutoOffsetReset>(o.AutoOffsetReset, ignoreCase: true, out _), "Kafka:Consumer:AutoOffsetReset must be Earliest, Latest or Error.")
            .ValidateOnStart();

        services.AddScoped<IKafkaMessageHandler, THandler>();
        services.AddHostedService<KafkaConsumerService>();
        return services;
    }

    /// <summary>Uses the application's existing <c>IConnectionMultiplexer</c> for duplicate suppression.</summary>
    public static IServiceCollection AddRedisIdempotencyStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        return services;
    }
}
