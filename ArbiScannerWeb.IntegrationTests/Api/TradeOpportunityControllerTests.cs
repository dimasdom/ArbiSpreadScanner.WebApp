using System.Net;
using System.Net.Http.Json;
using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.IntegrationTests.Fixtures;
using ArbiScannerWeb.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ArbiScannerWeb.IntegrationTests.Api;

[Collection(WebApiCollection.Name)]
public class TradeOpportunityControllerTests(WebApiTestFixture fixture)
{
    [Fact]
    public async Task GetSpreadsForUser_WithoutAuth_ReturnsUnauthorized()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/TradeOpportunity/GetSpreadsForUser");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSpreadsForUser_NoSpreadTypesEnabled_ReturnsEmptyList()
    {
        var client = await RegisterConfirmAndLoginAsync();

        var response = await client.GetAsync("/api/TradeOpportunity/GetSpreadsForUser");

        var result = await response.Content.ReadFromJsonAsync<ApiResult<List<TradeOpportunityDetailsDto>>>(JsonOptions.CaseInsensitive);
        result!.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSpreadInfo_ExistingSpread_ReturnsDetails()
    {
        var client = await RegisterConfirmAndLoginAsync();
        var spread = BuildSpread();

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var tradeOpportunityService = scope.ServiceProvider.GetRequiredService<ITradeOpportunityService>();
            (await tradeOpportunityService.AddSpread(spread)).IsSuccess.Should().BeTrue();
        }

        var response = await client.GetAsync($"/api/TradeOpportunity/GetSpreadInfo/{spread.Guid}");

        var result = await response.Content.ReadFromJsonAsync<ApiResult<TradeOpportunityDetailsDto>>(JsonOptions.CaseInsensitive);
        result!.IsSuccess.Should().BeTrue();
        result.Value!.PositionModel.Guid.Should().Be(spread.Guid);
        result.Value.PositionModel.Symbol.Should().Be(spread.Symbol);
    }

    [Fact]
    public async Task GetSpreadInfo_UnknownGuid_ReturnsNotFoundResult()
    {
        var client = await RegisterConfirmAndLoginAsync();

        var response = await client.GetAsync($"/api/TradeOpportunity/GetSpreadInfo/{Guid.NewGuid()}");

        var result = await response.Content.ReadFromJsonAsync<ApiResult<TradeOpportunityDetailsDto>>(JsonOptions.CaseInsensitive);
        result!.IsSuccess.Should().BeFalse();
    }

    private async Task<HttpClient> RegisterConfirmAndLoginAsync()
    {
        // CreateSecureClient(): the login cookie is Secure, so the client's CookieContainer
        // only re-sends it on a base address it considers HTTPS.
        var client = fixture.Factory.CreateSecureClient();
        var email = $"integration-{Guid.NewGuid():N}@example.com";
        const string password = "IntegrationTest@123";

        var registerResponse = await client.PostAsJsonAsync("/api/Account/Register", new AccountLoginDto { Login = email, Password = password });
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<ApiResult<EmailConfirmationCodes>>(JsonOptions.CaseInsensitive);

        await client.PostAsJsonAsync("/api/Account/ConfirmEmail", new ConfirmEmailDto
        {
            EmailConfirmToken = registerResult!.Value!.Id.ToString(),
            Token = registerResult.Value.Code
        });

        await client.PostAsJsonAsync("/api/Account/Login", new AccountLoginDto { Login = email, Password = password });

        return client;
    }

    private static TradeOpportunityModel BuildSpread()
    {
        ExchangeRateModel Rate(string exchange) => new()
        {
            Symbol = "BTC/USDT",
            Exchange = exchange,
            ExchangeRate = 50_000,
            VolumeAsk = 100,
            VolumeBid = 100
        };

        return new TradeOpportunityModel
        {
            Guid = Guid.NewGuid(),
            Symbol = "BTC/USDT",
            Type = SpreadType.Spot,
            ActionType = MarketPositionAction.Open,
            ExchangeRateA = Rate("Binance"),
            ExchangeRateB = Rate("Bybit"),
            ExchangeShort = Rate("Binance"),
            ExchangeLong = Rate("Bybit"),
            Spread = 1.5,
            StartSpread = 1.5,
            SummaryTarrif = 0.1,
            PossibleProfit = 10,
        };
    }
}
