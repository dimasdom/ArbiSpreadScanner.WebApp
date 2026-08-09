using FluentResults;
using System.Threading;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Abstractions.Interfaces
{
    public interface IMcpTokenService
    {
        /// <summary>
        /// Exchanges the current request's own Keycloak access token for a long-lived
        /// (30-day) access token scoped to the arbiscanner-mcp client, so the caller can
        /// use it as a standing bearer credential with ArbiScanner.McpServer.
        /// </summary>
        public Task<Result<string>> GenerateAccessTokenAsync(CancellationToken cancellationToken);
    }
}
