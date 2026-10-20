using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArbiScannerWeb.Infrastructure.Realtime;

public static class RealtimeServiceCollectionExtensions
{
    /// <summary>Registers the SSE pipeline: the broker (singleton, shared by the Kafka consumer and the HTTP streams), the
    /// <see cref="IRealtimeNotifier"/> that feeds it, and the stream service.</summary>
    public static IServiceCollection AddSpreadStream(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SpreadStreamOptions>(configuration.GetSection(SpreadStreamOptions.SectionName));
        services.AddSingleton<ISpreadStreamBroker, SpreadStreamBroker>();
        services.AddSingleton<IRealtimeNotifier, SseNotifier>();
        services.AddScoped<ISpreadStreamService, SpreadStreamService>();
        return services;
    }
}
