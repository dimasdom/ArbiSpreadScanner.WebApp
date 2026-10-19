using System.Diagnostics.Metrics;

namespace ArbiScannerWeb.Infrastructure.Realtime;

/// <summary>SSE stream counters. Register with <c>.AddMeter(SpreadStreamMetrics.MeterName)</c>.</summary>
public static class SpreadStreamMetrics
{
    public const string MeterName = "ArbiScanner.Realtime.SpreadStream";

    private static readonly Meter Meter = new(MeterName);

    private static long _activeStreams;

    public static readonly ObservableGauge<long> ActiveStreams = Meter.CreateObservableGauge(
        "arbiscanner.realtime.spread_stream.active",
        () => Interlocked.Read(ref _activeStreams),
        description: "Currently open spread SSE streams.");

    public static readonly Counter<long> EventsDropped = Meter.CreateCounter<long>(
        "arbiscanner.realtime.spread_stream.events_dropped",
        description: "Events discarded because a slow client's buffer was full.");

    internal static void StreamOpened() => Interlocked.Increment(ref _activeStreams);

    internal static void StreamClosed() => Interlocked.Decrement(ref _activeStreams);
}
