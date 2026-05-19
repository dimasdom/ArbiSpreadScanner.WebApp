using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ArbiScannerWeb.API.Filters;

public class ResultFailLoggingFilter : IAsyncActionFilter
{
    private readonly ILogger<ResultFailLoggingFilter> _logger;

    // Cached reflected property lookups for SerializableResult<T>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, (System.Reflection.PropertyInfo? IsSuccess, System.Reflection.PropertyInfo? Reasons)>
        _propertyCache = new();

    public ResultFailLoggingFilter(ILogger<ResultFailLoggingFilter> logger)
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

        var props = _propertyCache.GetOrAdd(type, t =>
        {
            var isSuccess = t.GetProperty("IsSuccess");
            var reasons = t.GetProperty("Reasons");
            return (isSuccess, reasons);
        });

        if (props.IsSuccess is null || props.Reasons is null)
            return;

        var isSuccess = props.IsSuccess.GetValue(value);
        if (isSuccess is not bool succeeded || succeeded)
            return;

        var reasons = props.Reasons.GetValue(value);
        var errorMessages = reasons switch
        {
            System.Collections.IEnumerable enumerable => string.Join("; ", enumerable.Cast<object>()
                .Select(r => r.GetType().GetProperty("Message")?.GetValue(r)?.ToString() ?? r.ToString())),
            _ => "unknown"
        };

        _logger.LogWarning(
            "Result.Fail on {Method} {Path} — {Errors}",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path,
            errorMessages);
    }
}
