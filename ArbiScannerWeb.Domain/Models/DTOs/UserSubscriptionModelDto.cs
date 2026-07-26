namespace ArbiScannerWeb.Domain.Models.DTOs;

public class UserSubscriptionModelDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int SubscriptionId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public SubscriptionModel? Subscription { get; set; }
    public bool IsActive { get; set; }

    public UserSubscriptionModelDto()
    {
    }

    public UserSubscriptionModelDto(UserSubscriptionModel userSubscriptionModel)
    {
        Id = userSubscriptionModel.Id;
        UserId = userSubscriptionModel.UserId;
        SubscriptionId = userSubscriptionModel.SubscriptionId;
        StartDate = userSubscriptionModel.StartDate;
        EndDate = userSubscriptionModel.EndDate;
        IsActive = userSubscriptionModel.EndDate > DateTime.UtcNow;
        Subscription = userSubscriptionModel.Subscription;
    }
}
