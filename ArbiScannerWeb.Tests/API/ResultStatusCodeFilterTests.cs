using ArbiScannerWeb.Infrastructure.Extensions;
using ArbiScannerWeb.Infrastructure.Filters;
using FluentAssertions;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace ArbiScannerWeb.Tests.API;

public class ResultStatusCodeFilterTests
{
    private static (ResultStatusCodeFilter Filter, Mock<ILogger<ResultStatusCodeFilter>> Logger) Create()
    {
        var logger = new Mock<ILogger<ResultStatusCodeFilter>>();
        return (new ResultStatusCodeFilter(logger.Object), logger);
    }

    private static ActionExecutingContext BuildExecutingContext(HttpContext? httpContext = null)
    {
        httpContext ??= new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/test";

        var actionContext = new ActionContext
        {
            HttpContext = httpContext,
            RouteData = new RouteData(),
            ActionDescriptor = new ActionDescriptor()
        };
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
            new Dictionary<string, object?>(), controller: new object());
    }

    private static ActionExecutionDelegate DelegateReturning(ActionExecutedContext executedCtx)
        => () => Task.FromResult(executedCtx);

    private static ActionExecutedContext BuildExecutedContext(object? resultValue, HttpContext? httpContext = null)
    {
        httpContext ??= new DefaultHttpContext();
        var actionContext = new ActionContext
        {
            HttpContext = httpContext,
            RouteData = new RouteData(),
            ActionDescriptor = new ActionDescriptor()
        };
        var ctx = new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), controller: new object());
        if (resultValue is not null)
            ctx.Result = new ObjectResult(resultValue);
        return ctx;
    }

    [Fact]
    public async Task OnActionExecutionAsync_SuccessResult_DoesNotLog()
    {
        var (filter, logger) = Create();
        var successObj = new SerializableResult<string> { IsSuccess = true };
        var executedCtx = BuildExecutedContext(successObj);
        var executingCtx = BuildExecutingContext(executedCtx.HttpContext);

        await filter.OnActionExecutionAsync(executingCtx, DelegateReturning(executedCtx));

        logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_FailResult_LogsWarningAndSetsStatusCode()
    {
        var (filter, logger) = Create();
        var failObj = new SerializableResult<string>
        {
            IsSuccess = false,
            Reasons = new List<IReason> { new Error("something went wrong") }
        };
        var executedCtx = BuildExecutedContext(failObj);
        var executingCtx = BuildExecutingContext(executedCtx.HttpContext);

        await filter.OnActionExecutionAsync(executingCtx, DelegateReturning(executedCtx));

        logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        ((ObjectResult)executedCtx.Result!).StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task OnActionExecutionAsync_FailResultWithStatusCodeMetadata_UsesThatStatusCode()
    {
        var (filter, _) = Create();
        var error = new Error("not found").WithMetadata("HttpStatusCode", 404);
        var failObj = new SerializableResult<string> { IsSuccess = false, Reasons = new List<IReason> { error } };
        var executedCtx = BuildExecutedContext(failObj);
        var executingCtx = BuildExecutingContext(executedCtx.HttpContext);

        await filter.OnActionExecutionAsync(executingCtx, DelegateReturning(executedCtx));

        ((ObjectResult)executedCtx.Result!).StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task OnActionExecutionAsync_NullObjectResult_SkipsLogging()
    {
        var (filter, logger) = Create();
        var executedCtx = BuildExecutedContext(resultValue: null);
        var executingCtx = BuildExecutingContext(executedCtx.HttpContext);

        await filter.OnActionExecutionAsync(executingCtx, DelegateReturning(executedCtx));

        logger.Verify(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ObjectWithoutIsSuccessProperty_SkipsLogging()
    {
        var (filter, logger) = Create();
        var plain = new { Name = "plain object without IsSuccess" };
        var executedCtx = BuildExecutedContext(plain);
        var executingCtx = BuildExecutingContext(executedCtx.HttpContext);

        await filter.OnActionExecutionAsync(executingCtx, DelegateReturning(executedCtx));

        logger.Verify(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
