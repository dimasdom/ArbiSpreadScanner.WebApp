using System.ComponentModel.DataAnnotations;

namespace ArbiScannerWeb.Domain.Models
{
    public class UserExchangeModel
    {
        [Key]
        public int Id { get; set; }
        public string UserAccountId { get; set; } = string.Empty;
        public int ExchangeId { get; set; }
        public ExchangeModel Exchange { get; set; } = default!;
    }
}