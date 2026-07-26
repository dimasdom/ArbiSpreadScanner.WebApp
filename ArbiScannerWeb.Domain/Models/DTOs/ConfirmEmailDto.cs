using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Domain.Models.DTOs
{
    public class ConfirmEmailDto
    {
        public string EmailConfirmToken { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }
}
