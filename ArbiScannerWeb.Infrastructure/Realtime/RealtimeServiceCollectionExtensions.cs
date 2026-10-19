using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArbiScannerWeb.Infrastructure.Realtime;

public static class RealtimeServiceCollectionExtensions
{
    /// <summary>Registers the SSE pipeline: broker (singleton, shared by the Kafka consumer and the HTTP streams), notifier and
    /// stream service. Does not touch <see cref="IRealtimeNotifier"/>; the host decides which notifier(s) feed it.</summary>
    public static IServiceCollection AddSpreadStream(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SpreadStreamOptions>(configuration.GetSection(SpreadStreamOptions.SectionName));
        services.AddSingleton<ISpreadStreamBroker, SpreadStreamBroker>();
        services.AddSingleton<SseNotifier>();
        services.AddScoped<ISpreadStreamService, SpreadStreamService>();
        return services;
    }
}
