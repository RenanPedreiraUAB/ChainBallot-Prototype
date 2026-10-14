using System.Security.Cryptography;

namespace CynetAuth.Api.Security;

/// <summary>
/// Implements password hashing with PBKDF2-SHA256 for local authentication.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int KeySize = 32;
    private const int SaltSize = 16;
    private const int Iterations = 100_000;

    /// <inheritdoc />
    public (byte[] Hash, byte[] Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return (hash, salt);
    }

    /// <inheritdoc />
    public bool VerifyPassword(string password, byte[] expectedHash, byte[] salt)
    {
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
