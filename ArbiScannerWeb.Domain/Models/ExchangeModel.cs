using System.ComponentModel.DataAnnotations;

namespace ArbiScannerWeb.Domain.Models
{
    public class ExchangeModel
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}