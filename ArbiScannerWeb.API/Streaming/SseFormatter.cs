using System.Text.Json;
using ArbiScannerWeb.Domain.Models.DTOs;

namespace ArbiScannerWeb.API.Streaming;

/// <summary>Writes <see cref="SpreadStreamEvent"/>s in the <c>text/event-stream</c> wire format. Event data is camelCase JSON, the
/// same casing the REST API and the SPA's types already use.</summary>
public static class SseFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The first thing written on a stream: sets the client's reconnect delay and flushes headers so <c>onopen</c>
    /// fires immediately instead of after the first event.</summary>
    public static string Preamble(TimeSpan reconnectDelay) =>
        $": connected\nretry: {(int)reconnectDelay.TotalMilliseconds}\n\n";

    public static string Format(SpreadStreamEvent streamEvent)
    {
        ArgumentNullException.ThrowIfNull(streamEvent);

        // A comment line: ignored by EventSource parsers, but traffic that stops idle proxies from timing the connection out.
        if (streamEvent.Type == SpreadStreamEventTypes.KeepAlive) return ": keep-alive\n\n";

        // Compact JSON never contains a raw newline, so `data:` always fits on one line.
        var data = JsonSerializer.Serialize(streamEvent.Data, JsonOptions);
        return streamEvent.Id is null
            ? $"event: {streamEvent.Type}\ndata: {data}\n\n"
            : $"id: {streamEvent.Id}\nevent: {streamEvent.Type}\ndata: {data}\n\n";
    }
}
