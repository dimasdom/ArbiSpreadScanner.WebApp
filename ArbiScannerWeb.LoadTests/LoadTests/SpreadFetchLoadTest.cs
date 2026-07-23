using ArbiScannerWeb.LoadTests.Settings;
using ArbiScannerWeb.LoadTests.Support;
using FluentAssertions;

namespace ArbiScannerWeb.LoadTests.LoadTests;

public class SpreadFetchLoadTest
{
    [SkippableFact]
    public async Task GetSpreadsForUser_UnderSustainedLoad_HasNoFailedRequests()
    {
        var settings = LoadTestSettings.FromEnvironment();
        Skip.IfNot(settings.IsConfigured, "WEB_LOADTEST_BASE_URL / WEB_LOADTEST_EMAIL / WEB_LOADTEST_PASSWORD are not set.");

        var session = await AuthenticatedClientFactory.CreateAsync(settings);
        using var client = session.Client;

        var result = await LoadRunner.RunAsync(
            async () =>
            {
                var response = await client.GetAsync("/api/TradeOpportunity/GetSpreadsForUser");
                return response.IsSuccessStatusCode;
            },
            settings.QueriesPerMinute,
            settings.Duration);

        result.OkCount.Should().BeGreaterThan(0, "at least one request should have succeeded");
        result.FailCount.Should().Be(0, "no request against GetSpreadsForUser should fail under the configured load");
    }
}
