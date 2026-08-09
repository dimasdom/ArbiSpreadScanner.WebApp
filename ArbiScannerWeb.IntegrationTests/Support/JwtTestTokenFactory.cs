using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ArbiScannerWeb.IntegrationTests.Support;

// Forges a JWT shaped like a real Keycloak-issued access token (sub/email/
// preferred_username claims) signed with JwtTestSettings' known test key.
internal static class JwtTestTokenFactory
{
    public static string CreateToken(string sub, string? email = null, string? preferredUsername = null)
    {
        var claims = new List<Claim> { new("sub", sub) };
        if (email != null)
        {
            claims.Add(new Claim("email", email));
        }
        if (preferredUsername != null)
        {
            claims.Add(new Claim("preferred_username", preferredUsername));
        }

        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(
            issuer: JwtTestSettings.Issuer,
            audience: JwtTestSettings.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(15),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.ASCII.GetBytes(JwtTestSettings.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
