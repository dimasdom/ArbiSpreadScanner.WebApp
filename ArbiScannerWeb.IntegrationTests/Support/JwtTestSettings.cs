namespace ArbiScannerWeb.IntegrationTests.Support;

internal static class JwtTestSettings
{
    public const string SigningKey = "integration-tests-signing-key-32-chars-minimum";
    public const string Issuer = "ArbiScannerWeb.IntegrationTests";
    public const string Audience = "ArbiScannerWeb.IntegrationTests";

    public static readonly Dictionary<string, string?> ConfigOverrides = new()
    {
        ["Jwt:SigningKey"] = SigningKey,
        ["Jwt:Issuer"] = Issuer,
        ["Jwt:Audience"] = Audience,
    };
}
