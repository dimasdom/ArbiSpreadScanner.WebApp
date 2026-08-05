namespace ArbiScannerWeb.LoadTests.Settings;

public sealed class LoadTestSettings
{
    public required string BaseUrl { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string OidcAuthority { get; init; }
    public required string OidcClientId { get; init; }
    public required int QueriesPerMinute { get; init; }
    public required TimeSpan Duration { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password)
        && !string.IsNullOrWhiteSpace(OidcAuthority) && !string.IsNullOrWhiteSpace(OidcClientId);

    public static LoadTestSettings FromEnvironment()
    {
        var queriesPerMinute = ReadInt("WEB_LOADTEST_QUERIES_PER_MINUTE", 60);
        var durationSeconds = ReadInt("WEB_LOADTEST_DURATION_SECONDS", 60);

        return new LoadTestSettings
        {
            BaseUrl = (Environment.GetEnvironmentVariable("WEB_LOADTEST_BASE_URL") ?? string.Empty).TrimEnd('/'),
            Email = Environment.GetEnvironmentVariable("WEB_LOADTEST_EMAIL") ?? string.Empty,
            Password = Environment.GetEnvironmentVariable("WEB_LOADTEST_PASSWORD") ?? string.Empty,
            OidcAuthority = (Environment.GetEnvironmentVariable("WEB_LOADTEST_OIDC_AUTHORITY") ?? string.Empty).TrimEnd('/'),
            OidcClientId = Environment.GetEnvironmentVariable("WEB_LOADTEST_OIDC_CLIENT_ID") ?? string.Empty,
            QueriesPerMinute = queriesPerMinute,
            Duration = TimeSpan.FromSeconds(durationSeconds)
        };
    }

    private static int ReadInt(string variable, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(variable);
        return int.TryParse(raw, out var value) && value > 0 ? value : fallback;
    }
}
