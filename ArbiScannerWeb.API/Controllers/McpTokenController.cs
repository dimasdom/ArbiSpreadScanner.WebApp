using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArbiScannerWeb.API.Controllers;

[ApiController]
[Authorize]
[Route("api/{controller}")]
public class McpTokenController(IMcpTokenService mcpTokenService) : ControllerBase
{
    [HttpPost("Generate")]
    public async Task<ActionResult<Result<string>>> Generate(CancellationToken cancellationToken)
    {
        return (await mcpTokenService.GenerateAccessTokenAsync(cancellationToken)).ToSerializable();
    }
}
