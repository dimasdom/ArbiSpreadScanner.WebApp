using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Domain.Models.DTOs
{
    public class ChangePasswordDto
    {
        public required string Token { get; set; }
        public required string NewPassword { get; set; }
    }
}
