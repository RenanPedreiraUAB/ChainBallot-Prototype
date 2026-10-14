using CynetAuth.Api.Data;

namespace CynetAuth.Api.Security;

/// <summary>
/// Defines JWT token creation for authenticated users.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Creates a signed JWT for the provided user account.
    /// </summary>
    /// <param name="user">Authenticated user account.</param>
    /// <returns>Signed token and expiration timestamp.</returns>
    (string Token, DateTimeOffset ExpiresAtUtc) CreateToken(UserCredential user);
}
