using System.Text.Json;

namespace ArbiScannerWeb.IntegrationTests.Support;

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };
}
