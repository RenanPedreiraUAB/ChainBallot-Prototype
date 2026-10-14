namespace CynetAuth.Api.Security;

/// <summary>
/// Defines password hashing and verification operations.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Creates a salted password hash.
    /// </summary>
    /// <param name="password">Plain-text password.</param>
    /// <returns>Hash and salt bytes.</returns>
    (byte[] Hash, byte[] Salt) HashPassword(string password);

    /// <summary>
    /// Verifies a plain-text password against stored hash and salt.
    /// </summary>
    /// <param name="password">Plain-text password.</param>
    /// <param name="expectedHash">Stored hash bytes.</param>
    /// <param name="salt">Stored salt bytes.</param>
    /// <returns>True when password is valid.</returns>
    bool VerifyPassword(string password, byte[] expectedHash, byte[] salt);
}
