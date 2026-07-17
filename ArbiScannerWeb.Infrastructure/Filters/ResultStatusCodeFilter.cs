using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace ArbiScannerWeb.Infrastructure.Filters;

public class ResultStatusCodeFilter : IAsyncActionFilter
{
    private readonly ILogger<ResultStatusCodeFilter> _logger;

    private static readonly ConcurrentDictionary<Type, ResultProperties> _propertyCache = new();

    public ResultStatusCodeFilter(ILogger<ResultStatusCodeFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        if (executed.Result is not ObjectResult { Value: not null } objectResult)
            return;

        var value = objectResult.Value;
        var type = value.GetType();

        var props = _propertyCache.GetOrAdd(type, t => new ResultProperties(
            t.GetProperty("IsSuccess"),
            t.GetProperty("Reasons"),
            t.GetProperty("ErrorCode"),
            t.GetProperty("Message")));

        if (props.IsSuccess is null || props.Reasons is null)
            return;

        var isSuccess = props.IsSuccess.GetValue(value);
        if (isSuccess is not bool succeeded || succeeded)
            return;

        var reasons = props.Reasons.GetValue(value) as IEnumerable;
        var firstReason = reasons?.Cast<object>().FirstOrDefault();

        var statusCode = GetHttpStatusCode(firstReason);
        objectResult.StatusCode = statusCode;

        var errorMessages = reasons is null
            ? "unknown"
            : string.Join("; ", reasons.Cast<object>()
                .Select(r => r.GetType().GetProperty("Message")?.GetValue(r)?.ToString() ?? r.ToString()));

        _logger.LogWarning(
            "Result.Fail ({StatusCode}) on {Method} {Path} — {Errors}",
            statusCode,
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path,
            errorMessages);
    }

    private static int GetHttpStatusCode(object? firstReason)
    {
        if (firstReason is null)
            return 400;

        var metadataProperty = firstReason.GetType().GetProperty("Metadata");
        if (metadataProperty?.GetValue(firstReason) is not IDictionary<string, object> metadata)
            return 400;

        return metadata.TryGetValue("HttpStatusCode", out var code) && code is int codeInt ? codeInt : 400;
    }

    private sealed record ResultProperties(
        PropertyInfo? IsSuccess,
        PropertyInfo? Reasons,
        PropertyInfo? ErrorCode,
        PropertyInfo? Message);
}
