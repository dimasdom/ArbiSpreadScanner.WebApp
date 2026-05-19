using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Domain.Models.DTOs
{
    public class AccountEditDTO
    {
        public AccountEditDTO() { }
        public string Email { get; set; } = default!;
        public double SpreadSize { get; set; } = 0;
        public int PositionSize { get; set; } = 0;
        public bool FuturesSpread { get; set; } = false;
        public bool FundingSpread { get; set; } = false;
        public bool SpotSpread { get; set; } = false;
        public List<UserExchangeModel> Exchanges { get; set; } = new();
    }
}
