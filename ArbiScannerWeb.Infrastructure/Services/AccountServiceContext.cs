using ArbiScannerWeb.Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace ArbiScannerWeb.Infrastructure.Services
{
    // Groups AccountService's ASP.NET Identity/HTTP framework dependencies so its
    // constructor stays within the parameter-count limit (S107).
    public class AccountServiceContext
    {
        public SignInManager<AccountModel> SignInManager { get; }
        public UserManager<AccountModel> UserManager { get; }
        public IHttpContextAccessor HttpContextAccessor { get; }
        public string ClientUrl { get; }

        public AccountServiceContext(
            SignInManager<AccountModel> signInManager,
            UserManager<AccountModel> userManager,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            SignInManager = signInManager;
            UserManager = userManager;
            HttpContextAccessor = httpContextAccessor;
            ClientUrl = configuration["ClientUrl"] ?? "http://localhost:3001";
        }
    }
}
