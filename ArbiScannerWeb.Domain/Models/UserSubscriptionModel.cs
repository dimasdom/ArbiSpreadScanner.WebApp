namespace ArbiScannerWeb.Domain.Models;

public class UserSubscriptionModel
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int SubscriptionId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public SubscriptionModel? Subscription { get; set; }
}
