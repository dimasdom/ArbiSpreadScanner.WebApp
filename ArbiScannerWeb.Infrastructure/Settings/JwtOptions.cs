namespace ArbiScannerWeb.Infrastructure.Settings
{
    /// <summary>
    /// Resource-server config for validating Keycloak-issued access tokens.
    /// Token issuance/refresh/lifetimes are owned by Keycloak, not this service.
    /// </summary>
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        /// <summary>
        /// The Keycloak realm issuer used for JWKS discovery, e.g.
        /// https://auth.arbiscannerwebapp.site/realms/arbiscanner-web
        /// </summary>
        public string Authority { get; set; } = string.Empty;

        /// <summary>
        /// The expected audience claim on incoming access tokens, e.g.
        /// arbiscanner-web-spa.
        /// </summary>
        public string Audience { get; set; } = string.Empty;
    }
}
