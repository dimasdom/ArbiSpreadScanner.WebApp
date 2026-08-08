using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArbiScannerWeb.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/{controller}")]
    public class McpTokenController : ControllerBase
    {
        private readonly IMcpTokenService _mcpTokenService;

        public McpTokenController(IMcpTokenService mcpTokenService)
        {
            _mcpTokenService = mcpTokenService;
        }

        [HttpPost("Generate")]
        public async Task<ActionResult<Result<string>>> Generate(CancellationToken cancellationToken)
        {
            return (await _mcpTokenService.GenerateAccessTokenAsync(cancellationToken)).ToSerializable();
        }
    }
}
