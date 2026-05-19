using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Domain.Models
{
    public class ForgetPasswordRequest
    {
        [Key]
        public Guid Id { get; set; }
        public required string Token { get; set; }
        public required string UserId { get; set; }
    }
}
