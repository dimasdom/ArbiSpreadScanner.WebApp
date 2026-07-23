using System.Net.Http.Json;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.LoadTests.Settings;
using ArbiScannerWeb.LoadTests.Support;
using FluentAssertions;

namespace ArbiScannerWeb.LoadTests.LoadTests;

public class AccountUpdateLoadTest
{
    [SkippableFact]
    public async Task UpdateDetails_UnderSustainedLoad_HasNoFailedRequests()
    {
        var settings = LoadTestSettings.FromEnvironment();
        Skip.IfNot(settings.IsConfigured, "WEB_LOADTEST_BASE_URL / WEB_LOADTEST_EMAIL / WEB_LOADTEST_PASSWORD are not set.");

        var session = await AuthenticatedClientFactory.CreateAsync(settings);
        using var client = session.Client;

        var userSettings = session.Account.UserSettings;
        var payload = new AccountEditDTO
        {
            Email = settings.Email,
            SpreadSize = userSettings.SpreadSize,
            PositionSize = userSettings.PositionSize,
            FuturesSpread = userSettings.FuturesSpread,
            FundingSpread = userSettings.FundingSpread,
            SpotSpread = userSettings.SpotSpread,
            Exchanges = userSettings.Exchanges
        };

        var result = await LoadRunner.RunAsync(
            async () =>
            {
                var response = await client.PostAsJsonAsync("/api/Account/UpdateDetails", payload);
                return response.IsSuccessStatusCode;
            },
            settings.QueriesPerMinute,
            settings.Duration);

        result.OkCount.Should().BeGreaterThan(0, "at least one request should have succeeded");
        result.FailCount.Should().Be(0, "no request against UpdateDetails should fail under the configured load");
    }
}
