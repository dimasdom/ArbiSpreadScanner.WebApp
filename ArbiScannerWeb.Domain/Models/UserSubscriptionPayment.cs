namespace ArbiScannerWeb.Domain.Models;

public class UserSubscriptionPayment
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int SubscriptionId { get; set; }
    public int PaymentId { get; set; }
    public DateTime ExpirationDate { get; set; }
    public SubscriptionModel? Subscription { get; set; }
    public PaymentModel? Payment { get; set; }
}
