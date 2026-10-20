using System.Net;
using System.Text.Json;
using ArbiScannerWeb.LoadTests.Settings;
using ArbiScannerWeb.LoadTests.Support;
using FluentAssertions;

namespace ArbiScannerWeb.LoadTests.LoadTests;

public class SpreadStreamLoadTest
{
    private static readonly TimeSpan PreambleTimeout = TimeSpan.FromSeconds(10);

    [SkippableFact]
    public async Task OpeningSpreadStreams_UnderSustainedLoad_HasNoFailedRequests()
    {
        var settings = LoadTestSettings.FromEnvironment();
        Skip.IfNot(settings.IsConfigured, "WEB_LOADTEST_BASE_URL / WEB_LOADTEST_EMAIL / WEB_LOADTEST_PASSWORD are not set.");

        var session = await AuthenticatedClientFactory.CreateAsync(settings);
        using var client = session.Client;

        var spreadId = await FindAnOpenSpreadAsync(client);
        Skip.If(spreadId is null, "No open spread is available to stream; start the scanner or seed one.");

        // Each request authenticates, passes the subscription check, subscribes to the broker and receives the stream preamble
        // (which the server flushes before any event), then disconnects. Measures connect throughput and that closing streams
        // does not leak subscribers; an idle spread would otherwise give nothing to read.
        var result = await LoadRunner.RunAsync(
            async () =>
            {
                using var cts = new CancellationTokenSource(PreambleTimeout);
                using var response = await client.GetAsync($"/api/TradeOpportunity/Stream/{spreadId}", HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentType?.MediaType != "text/event-stream") return false;

                using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cts.Token));
                var firstLine = await reader.ReadLineAsync(cts.Token);
                return firstLine is not null && firstLine.StartsWith(':');
            },
            settings.QueriesPerMinute,
            settings.Duration);

        result.OkCount.Should().BeGreaterThan(0, "at least one stream should have opened");
        result.FailCount.Should().Be(0, "no stream should fail to open under the configured load");
    }

    private static async Task<Guid?> FindAnOpenSpreadAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/TradeOpportunity/GetSpreadsForUser");
        if (!response.IsSuccessStatusCode) return null;

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        if (!json.RootElement.TryGetProperty("value", out var spreads) || spreads.ValueKind != JsonValueKind.Array) return null;

        foreach (var spread in spreads.EnumerateArray())
        {
            if (spread.TryGetProperty("positionModel", out var model) && model.TryGetProperty("guid", out var guid) && guid.TryGetGuid(out var id))
            {
                return id;
            }
        }

        return null;
    }
}
