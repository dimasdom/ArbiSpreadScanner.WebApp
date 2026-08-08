using ArbiScannerWeb.Abstractions.Interfaces;
using ArbiScannerWeb.Infrastructure.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Infrastructure.Services
{
    public class McpTokenService : IMcpTokenService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ILogger<McpTokenService> _logger;

        public McpTokenService(
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ILogger<McpTokenService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("KeycloakMcpExchange");
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<Result<string>> GenerateAccessTokenAsync(CancellationToken cancellationToken)
        {
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Fail(TypedErrors.Unauthorized("No bearer token on the current request."));
            }

            var subjectToken = authHeader["Bearer ".Length..];
            var clientId = _configuration["Keycloak:McpExchange:ClientId"];
            var clientSecret = _configuration["Keycloak:McpExchange:ClientSecret"];

            // Standard Token Exchange (RFC 8693, Keycloak 26.2+) — exchanges the caller's
            // own arbiscanner-web-spa session token for an access token scoped to
            // arbiscanner-mcp (carrying both arbiscanner-mcp and arbiscanner-web-spa in its
            // aud claim, via the mappers on that client — see keycloak/README.md step 9).
            //
            // This is deliberately NOT an offline/refresh token. Requesting
            // requested_token_type=refresh_token is rejected by Keycloak 26.4 with
            // invalid_request/"requested_token_type unsupported", and even requesting the
            // offline_access scope on a plain exchange comes back with
            // refresh_expires_in=0 — no refresh token issued at all (confirmed against a
            // real container, not assumed; Standard Token Exchange apparently doesn't
            // support minting offline tokens as of this Keycloak version). Instead, the
            // arbiscanner-mcp client itself carries an access.token.lifespan override
            // (2592000s / 30 days, see the realm export) — the exchanged access token is
            // simply long-lived by construction, which is what makes it usable as a
            // standing bearer credential in an MCP client's static config. It's not
            // exchanged again by anything downstream; ArbiScanner.McpServer just forwards
            // it as-is.
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:token-exchange",
                ["client_id"] = clientId ?? string.Empty,
                ["client_secret"] = clientSecret ?? string.Empty,
                ["subject_token"] = subjectToken,
                ["subject_token_type"] = "urn:ietf:params:oauth:token-type:access_token",
                // offline_access doesn't get us a refresh_token here (Standard Token
                // Exchange doesn't issue one — see above), but it's still what's needed
                // to make the exchanged access token's own session an OFFLINE session,
                // which is what lets access.token.lifespan actually govern instead of
                // being capped by the realm's ~10h SSO Session Max (confirmed against a
                // real container: dropping this scope silently truncated the token back
                // down to the session-bound lifetime, even with the same client override).
                ["scope"] = "openid offline_access",
            });

            try
            {
                var response = await _httpClient.PostAsync("protocol/openid-connect/token", form, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Keycloak token exchange failed with {StatusCode}: {Body}",
                        response.StatusCode, body);
                    return Result.Fail(TypedErrors.InternalError("Could not generate an MCP token. Please try again later."));
                }

                var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                var token = payload.TryGetProperty("access_token", out var tokenElement) ? tokenElement.GetString() : null;
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError("Keycloak token exchange response did not contain an access_token: {Payload}", payload.GetRawText());
                    return Result.Fail(TypedErrors.InternalError("Could not generate an MCP token. Please try again later."));
                }

                return Result.Ok(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exchanging token with Keycloak for an MCP token");
                return Result.Fail(TypedErrors.InternalError("Could not generate an MCP token. Please try again later."));
            }
        }
    }
}
