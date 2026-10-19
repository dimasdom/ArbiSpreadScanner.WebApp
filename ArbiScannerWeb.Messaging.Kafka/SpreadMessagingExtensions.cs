using ArbiScannerWeb.Abstractions.Messaging;
using ArbiScannerWeb.Infrastructure.Messaging;
using ArbiScannerWeb.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArbiScannerWeb.Messaging.Kafka;

public static class SpreadMessagingExtensions
{
    /// <summary>
    /// Wires a spread-consuming service to Kafka: producer (for dead-lettering), duplicate suppression, and one consumer in group
    /// <c>{Messaging:ServiceName}-spreads</c> that hands every message to <typeparamref name="THandler"/> (scoped, resolved per message).
    /// Everything is bound lazily from configuration, so test hosts can override it. The host must already provide an
    /// <c>IConnectionMultiplexer</c> and should add <see cref="KafkaHealthCheck"/>.
    /// </summary>
    public static IServiceCollection AddSpreadMessaging<THandler>(this IServiceCollection services, IConfiguration configuration)
        where THandler : class, IKafkaMessageHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<MessagingOptions>().Bind(configuration.GetSection(MessagingOptions.SectionName));

        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        services.AddKafkaMessaging(configuration);
        services.AddKafkaConsumer<THandler>(configuration);
        services.AddOptions<KafkaConsumerOptions>()
            .Configure<IOptions<MessagingOptions>>((consumer, messaging) => consumer.GroupId = LiveGroupId(messaging.Value.ServiceName));

        return services;
    }

    /// <summary>Empty when no service name is configured, which fails consumer validation with a clear message.</summary>
    public static string LiveGroupId(string serviceName) =>
        string.IsNullOrWhiteSpace(serviceName) ? string.Empty : $"{serviceName}-spreads";
}
