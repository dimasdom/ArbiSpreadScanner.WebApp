using System.ComponentModel.DataAnnotations;

namespace ArbiScannerWeb.Domain.Models
{
    public class ExchangeLinkModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Exchange { get; set; } = string.Empty;

        public string Spot { get; set; } = string.Empty;

        public string Futures { get; set; } = string.Empty;

        public string Funding { get; set; } = string.Empty;

        /// <summary>
        /// Builds the trading URL for this exchange given a symbol and spread type.
        /// Symbol format: "BTC/USDT" or "BTC/USDT:USDT" — base is extracted before '/' and quote between '/' and ':'.
        /// </summary>
        public string BuildUrl(string symbol, SpreadType type)
        {
            var parts = symbol.Split('/');
            var baseAsset = parts[0];
            var quotePart = parts.Length > 1 ? parts[1].Split(':')[0] : "USDT";

            var template = type switch
            {
                SpreadType.Spot => Spot,
                SpreadType.Funding => Funding,
                _ => Futures
            };

            return template
                .Replace("{base}", baseAsset)
                .Replace("{quote}", quotePart);
        }
    }
}
