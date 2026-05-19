namespace ArbiScannerWeb.Domain.Models;

public class SubscriptionModel
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationInDays { get; set; }
}
