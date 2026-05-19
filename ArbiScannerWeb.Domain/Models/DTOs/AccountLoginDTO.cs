using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Domain.Models.DTOs
{
    public class AccountLoginDTO
    {
        public required string Login { get; set; }
        public required string Password { get; set; }
    }
}
