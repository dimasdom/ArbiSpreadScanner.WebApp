namespace ArbiScannerWeb.Domain.Models.DTOs
{
    public class AccountDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public UserSettingsModel UserSettings { get; set; } = new();
    }
}
