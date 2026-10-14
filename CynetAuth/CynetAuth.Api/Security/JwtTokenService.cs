using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CynetAuth.Api.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CynetAuth.Api.Security;

/// <summary>
/// Creates signed JWT access tokens for authenticated local users.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    /// <inheritdoc />
    public (string Token, DateTimeOffset ExpiresAtUtc) CreateToken(UserCredential user)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // Keep subject and unique_name explicit so Authorization component can
        // deterministically map identity to VID without leaking raw credentials.
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString("N")),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim("amr", "pwd"),
            new Claim("auth_time", now.ToUnixTimeSeconds().ToString())
        };

        var jwtToken = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(jwtToken), expires);
    }
}
