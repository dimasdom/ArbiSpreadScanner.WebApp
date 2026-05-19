using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Domain.Models
{
    public class AccountModel : IdentityUser
    {
        public int UserSettingsId { get; set; }
        public UserSettingsModel UserSettings { get; set; } = new();
        
        /// <summary>
        /// Navigation property for all refresh tokens issued to this user.
        /// </summary>
        public virtual ICollection<RefreshTokenModel> RefreshTokens { get; set; } = new List<RefreshTokenModel>();
    }
}
