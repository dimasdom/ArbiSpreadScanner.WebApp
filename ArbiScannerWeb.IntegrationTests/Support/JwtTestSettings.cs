using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ArbiScannerWeb.IntegrationTests.Support;

// Fast, no-Keycloak auth path for most integration tests: forge a JWT signed with a
// known test key instead of validating against a real Authority/JWKS endpoint. See
// KeycloakDiscoveryTests for the one test that exercises real Authority-based
// discovery against a live Testcontainers.Keycloak instance.
internal static class JwtTestSettings
{
    public const string SigningKey = "integration-tests-signing-key-32-chars-minimum";
    public const string Issuer = "ArbiScannerWeb.IntegrationTests";
    public const string Audience = "ArbiScannerWeb.IntegrationTests";

    public static readonly Dictionary<string, string?> ConfigOverrides = new()
    {
        ["Jwt:Authority"] = string.Empty,
        ["Jwt:Audience"] = Audience,
    };

    // Overrides the JwtBearer options AddJwtBearer configured from Jwt:Authority with a
    // static symmetric key, mirroring what a real Keycloak-issued token validates
    // against but without any network dependency.
    public static void ConfigureTestJwtBearer(IServiceCollection services)
    {
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Authority = null;
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(SigningKey)),
                ValidateIssuerSigningKey = true,
                NameClaimType = "sub",
            };
        });
    }
}
