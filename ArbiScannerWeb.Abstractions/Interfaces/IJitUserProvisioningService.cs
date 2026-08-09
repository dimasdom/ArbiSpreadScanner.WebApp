using System.Security.Claims;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Abstractions.Interfaces
{
    /// <summary>
    /// Ensures a local AccountModel/UserSettings row exists for a Keycloak-authenticated
    /// principal, creating one on first sight. Keycloak owns identity; this is the local
    /// shadow record the rest of the app's business tables key off.
    /// </summary>
    public interface IJitUserProvisioningService
    {
        Task EnsureProvisionedAsync(ClaimsPrincipal user);
    }
}
