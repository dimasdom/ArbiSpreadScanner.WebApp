namespace ArbiScannerWeb.Domain.Models;
public class TelegramLinkRequest
{
    public Guid Id { get; set; }
    public string AccountId {get ;set;} = default!;
}