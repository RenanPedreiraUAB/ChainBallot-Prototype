using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ElectionAuthorization.Api.Tests;

/// <summary>
/// Creates test JWT tokens that match Election Authorization trusted auth settings.
/// </summary>
public static class TestJwtFactory
{
    public static string Create(string subject, string username = "test-user")
    {
        var now = DateTimeOffset.UtcNow;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("CHANGE_ME_FOR_DEV_ONLY_CYNETAUTH_KEY_1234567890"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "cynetauth",
            audience: "chainballot-authz",
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(JwtRegisteredClaimNames.UniqueName, username)
            },
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(30).UtcDateTime,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
