using System.Net;
using ArbiScannerWeb.API.Filters;
using FluentAssertions;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ArbiScannerWeb.Tests.API;

public class HangfireLocalRequestFilterTests
{
    private static AspNetCoreDashboardContext BuildContext(IPAddress? remoteIp)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        httpContext.Connection.RemoteIpAddress = remoteIp;
        var storage = new Mock<JobStorage>();
        return new AspNetCoreDashboardContext(storage.Object, new DashboardOptions(), httpContext);
    }

    [Fact]
    public void Authorize_LoopbackAddress_ReturnsTrue()
    {
        var filter = new HangfireLocalRequestFilter();
        var context = BuildContext(IPAddress.Loopback);

        var result = filter.Authorize(context);

        result.Should().BeTrue();
    }

    [Fact]
    public void Authorize_NonLoopbackAddress_ReturnsFalse()
    {
        var filter = new HangfireLocalRequestFilter();
        var context = BuildContext(IPAddress.Parse("203.0.113.5"));

        var result = filter.Authorize(context);

        result.Should().BeFalse();
    }

    [Fact]
    public void Authorize_NullRemoteAddress_ReturnsFalse()
    {
        var filter = new HangfireLocalRequestFilter();
        var context = BuildContext(null);

        var result = filter.Authorize(context);

        result.Should().BeFalse();
    }
}
