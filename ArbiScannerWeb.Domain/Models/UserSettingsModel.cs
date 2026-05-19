using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Domain.Models
{
    public class UserSettingsModel 
    {
        [Key]
        public int Id { get; set; }
        public string AccountId { get; set; } = default!;
        public string? UserName { get; set; } = default!;
        public long ChatId { get; set; } = 0;
        public double SpreadSize { get; set; } = 0;
        public int PositionSize { get; set; } = 0;
        public bool FuturesSpread { get; set; } = false;
        public bool FundingSpread { get; set; } = false;
        public bool SpotSpread { get; set; } = false;
        public bool HaveAccess { get; set; } = false;
        public bool Active { get; set; } = false;
        public List<UserExchangeModel> Exchanges { get; set; } = new();
        public static bool IsVolumeValid(UserSettingsModel user, TradeOpportunityModel tradeOpportunityModel)
        {
            var neededVolume = user.PositionSize * 3;
            return tradeOpportunityModel.ExchangeRateA.VolumeAsk > neededVolume &&
                tradeOpportunityModel.ExchangeRateA.VolumeBid > neededVolume &&
                tradeOpportunityModel.ExchangeRateB.VolumeAsk > neededVolume &&
                tradeOpportunityModel.ExchangeRateB.VolumeBid > neededVolume;
        }

        public static IQueryable<TradeOpportunityModel> IsVolumeValidQuery(UserSettingsModel user, IQueryable<TradeOpportunityModel> query)
        {
            var neededVolume = user.PositionSize * 3;
            return query.Where(tradeOpportunityModel => tradeOpportunityModel.ExchangeRateA.VolumeAsk > neededVolume &&
                tradeOpportunityModel.ExchangeRateA.VolumeBid > neededVolume &&
                tradeOpportunityModel.ExchangeRateB.VolumeAsk > neededVolume &&
                tradeOpportunityModel.ExchangeRateB.VolumeBid > neededVolume);
        }
    }
}
