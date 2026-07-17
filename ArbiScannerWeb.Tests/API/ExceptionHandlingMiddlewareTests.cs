using ArbiScannerWeb.Infrastructure.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArbiScannerWeb.Tests.API;

public class ExceptionHandlingMiddlewareTests
{
    private static ExceptionHandlingMiddleware CreateSut(RequestDelegate next) =>
        new(next, NullLogger<ExceptionHandlingMiddleware>.Instance);

    private static DefaultHttpContext CreateContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    [Fact]
    public async Task InvokeAsync_NoException_CallsNextAndLeavesStatusUntouched()
    {
        var called = false;
        var sut = CreateSut(_ => { called = true; return Task.CompletedTask; });
        var ctx = CreateContext();

        await sut.InvokeAsync(ctx);

        called.Should().BeTrue();
        ctx.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_ExceptionThrown_Sets500StatusCode()
    {
        var sut = CreateSut(_ => throw new InvalidOperationException("boom"));
        var ctx = CreateContext();

        await sut.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task InvokeAsync_ExceptionThrown_WritesJsonBodyWithApplicationJsonContentType()
    {
        var sut = CreateSut(_ => throw new Exception("boom"));
        var ctx = CreateContext();

        await sut.InvokeAsync(ctx);

        ctx.Response.ContentType.Should().Be("application/json");
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.Should().Contain("isSuccess");
    }

    [Fact]
    public async Task InvokeAsync_ExceptionThrown_DoesNotPropagate()
    {
        var sut = CreateSut(_ => throw new Exception("secret internal error"));
        var ctx = CreateContext();

        var act = async () => await sut.InvokeAsync(ctx);

        await act.Should().NotThrowAsync();
    }
}
