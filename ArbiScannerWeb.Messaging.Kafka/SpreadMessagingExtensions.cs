using ArbiScannerWeb.Abstractions.Messaging;
using ArbiScannerWeb.Infrastructure.Messaging;
using ArbiScannerWeb.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArbiScannerWeb.Messaging.Kafka;

public static class SpreadMessagingExtensions
{
    /// <summary>
    /// Wires the Kafka side of a spread-consuming service according to <c>Messaging:Consumer:Mode</c>. The existing RabbitMQ
    /// consumer is registered by the host as before and is untouched in every mode except <c>Kafka</c>.
    /// <list type="bullet">
    /// <item><c>RabbitMq</c> (default): nothing Kafka-related runs; parity recording is a no-op.</item>
    /// <item><c>Shadow</c>: a Kafka consumer in group <c>{ServiceName}-spreads-shadow</c> processes the same events with no side
    /// effects, and both backends are recorded and compared per minute.</item>
    /// <item><c>Kafka</c>: rejected at startup until the cutover phase.</item>
    /// </list>
    /// Deliberately registers no Kafka health check: a Kafka outage must not mark the service unhealthy while RabbitMQ is live.
    /// The host must already provide an <c>IConnectionMultiplexer</c>.
    /// </summary>
    public static IServiceCollection AddSpreadMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(MessagingOptions.SectionName);
        var options = section.Get<MessagingOptions>() ?? new MessagingOptions();

        services.AddOptions<MessagingOptions>().Bind(section);

        switch (options.Consumer.Mode)
        {
            case MessagingConsumerMode.RabbitMq:
                services.TryAddSingleton<IParityRecorder, NoOpParityRecorder>();
                break;

            case MessagingConsumerMode.Shadow:
                if (string.IsNullOrWhiteSpace(options.ServiceName))
                {
                    throw new InvalidOperationException("Messaging:ServiceName is required when Messaging:Consumer:Mode is Shadow.");
                }

                services.AddSingleton<IParityRecorder, RedisParityRecorder>();
                services.AddHostedService<ParityComparerService>();
                services.AddKafkaMessaging(configuration);
                services.AddKafkaConsumer<ShadowSpreadHandler>(configuration);
                services.PostConfigure<KafkaConsumerOptions>(o => o.GroupId = ShadowGroupId(options.ServiceName));
                break;

            case MessagingConsumerMode.Kafka:
                throw new NotSupportedException("Messaging:Consumer:Mode=Kafka is not available yet; it arrives with the cutover phase. Use RabbitMq or Shadow.");

            default:
                throw new NotSupportedException($"Unknown Messaging:Consumer:Mode '{options.Consumer.Mode}'.");
        }

        return services;
    }

    public static string ShadowGroupId(string serviceName) => $"{serviceName}-spreads-shadow";
}
