namespace ArbiScannerWeb.Infrastructure.Settings;
public class RabbitMqSettings
{
    public string Host { get; set; } = default!;
    public string Queue { get; set; } = default!;
    public string Exchange { get; set; } = default!;
    public string RoutingKey { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
}