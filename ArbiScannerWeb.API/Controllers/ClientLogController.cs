using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArbiScannerWeb.API.Controllers;

public record FrontendLogEntry(
    string Level,
    string Message,
    string? Source,
    DateTimeOffset Timestamp,
    string? Details
);

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class ClientLogController : ControllerBase
{
    private readonly ILogger<ClientLogController> _logger;

    public ClientLogController(ILogger<ClientLogController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public IActionResult Log([FromBody] IEnumerable<FrontendLogEntry> entries)
    {
        foreach (var entry in entries)
        {
            using (_logger.BeginScope(new Dictionary<string, object> { ["source"] = "frontend" }))
            {
                var level = entry.Level.ToLowerInvariant() switch
                {
                    "error" => LogLevel.Error,
                    "warn" or "warning" => LogLevel.Warning,
                    "debug" => LogLevel.Debug,
                    _ => LogLevel.Information
                };

                _logger.Log(level,
                    "[Frontend] {Message} | Origin: {Source} | ClientTime: {Timestamp} | Details: {Details}",
                    entry.Message,
                    entry.Source ?? "unknown",
                    entry.Timestamp,
                    entry.Details ?? "-");
            }
        }

        return Ok();
    }
}
