namespace ArbiScannerWeb.LoadTests.Settings;

public sealed class LoadTestSettings
{
    public required string BaseUrl { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required int QueriesPerMinute { get; init; }
    public required TimeSpan Duration { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);

    public int RequestsPerSecond => Math.Max(1, (int)Math.Ceiling(QueriesPerMinute / 60.0));

    public static LoadTestSettings FromEnvironment()
    {
        var queriesPerMinute = ReadInt("WEB_LOADTEST_QUERIES_PER_MINUTE", 60);
        var durationSeconds = ReadInt("WEB_LOADTEST_DURATION_SECONDS", 60);

        return new LoadTestSettings
        {
            BaseUrl = (Environment.GetEnvironmentVariable("WEB_LOADTEST_BASE_URL") ?? string.Empty).TrimEnd('/'),
            Email = Environment.GetEnvironmentVariable("WEB_LOADTEST_EMAIL") ?? string.Empty,
            Password = Environment.GetEnvironmentVariable("WEB_LOADTEST_PASSWORD") ?? string.Empty,
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
