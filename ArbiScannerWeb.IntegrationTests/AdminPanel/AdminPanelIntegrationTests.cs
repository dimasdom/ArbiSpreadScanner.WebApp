using System.Net;
using System.Net.Http.Json;
using ArbiScannerWeb.Domain.Models;
using ArbiScannerWeb.Domain.Models.DTOs;
using ArbiScannerWeb.IntegrationTests.Fixtures;
using ArbiScannerWeb.IntegrationTests.Support;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace ArbiScannerWeb.IntegrationTests.AdminPanel;

[Collection(AdminPanelCollection.Name)]
public class AdminPanelIntegrationTests(AdminPanelTestFixture fixture)
{
    [Fact]
    public async Task GetAllSubscriptions_ReturnsTiersFromAdminPanel()
    {
        fixture.AdminPanelApi
            .Given(Request.Create().WithPath("/api/subscriptions/GetAllSubscriptions").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    isSuccess = true,
                    value = new object[]
                    {
                        new { id = 1, type = "Basic", price = 9.99m, durationInDays = 30 },
                        new { id = 2, type = "Standard", price = 19.99m, durationInDays = 90 },
                        new { id = 3, type = "Premium", price = 49.99m, durationInDays = 365 },
                    }
                }));

        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/Subscription/GetAllSubscriptions");

        var result = await response.Content.ReadFromJsonAsync<ApiResult<List<SubscriptionModel>>>(JsonOptions.CaseInsensitive);
        result!.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(s => s.Type == "Basic" && s.DurationInDays == 30);
        result.Value.Should().Contain(s => s.Type == "Standard" && s.DurationInDays == 90);
        result.Value.Should().Contain(s => s.Type == "Premium" && s.DurationInDays == 365);
    }

    [Fact]
    public async Task GetUserActiveSubscriptions_AdminPanelReturnsNotFound_PropagatesFailure()
    {
        var client = fixture.Factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/Subscription/GetUserActiveSubscriptions");

        var result = await response.Content.ReadFromJsonAsync<ApiResult<UserSubscriptionModelDto>>(JsonOptions.CaseInsensitive);
        result!.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task AdminService_OnUnauthorized_ReAuthenticatesAndRetriesOnce()
    {
        const int subscriptionId = 999;
        const string route = "/api/subscriptions/GetSubscriptionById";

        fixture.AdminPanelApi
            .Given(Request.Create().WithPath(route).WithParam("id", subscriptionId.ToString()).UsingGet())
            .InScenario("retry-on-401")
            .WillSetStateTo("retried")
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Unauthorized));

        fixture.AdminPanelApi
            .Given(Request.Create().WithPath(route).WithParam("id", subscriptionId.ToString()).UsingGet())
            .InScenario("retry-on-401")
            .WhenStateIs("retried")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { isSuccess = true, value = new { id = subscriptionId, type = "RetryTier", price = 1.0m, durationInDays = 1 } }));

        var client = fixture.Factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/Subscription/GetSubscriptionDetails?subscriptionId={subscriptionId}");

        var result = await response.Content.ReadFromJsonAsync<ApiResult<SubscriptionModel>>(JsonOptions.CaseInsensitive);
        result!.IsSuccess.Should().BeTrue("AdminService should have retried transparently after the 401");
        result.Value!.Type.Should().Be("RetryTier");
    }
}
