namespace ArbiScannerWeb.LoadTests.Support;

internal sealed class ApiResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Value { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
}
