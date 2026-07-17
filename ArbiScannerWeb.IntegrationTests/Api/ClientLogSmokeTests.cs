using System.Net;
using System.Net.Http.Json;
using ArbiScannerWeb.API.Controllers;
using ArbiScannerWeb.IntegrationTests.Fixtures;
using FluentAssertions;

namespace ArbiScannerWeb.IntegrationTests.Api;

[Collection(WebApiCollection.Name)]
public class ClientLogSmokeTests(WebApiTestFixture fixture)
{
    [Fact]
    public async Task PostClientLog_HostIsUp_ReturnsOk()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/ClientLog", new[]
        {
            new FrontendLogEntry("info", "integration test smoke ping", "ClientLogSmokeTests", DateTimeOffset.UtcNow, null)
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
