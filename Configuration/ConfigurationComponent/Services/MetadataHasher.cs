using System.Security.Cryptography;
using System.Text;

namespace ConfigurationComponent.Services;

/// <summary>
/// Computes the SHA-256 hash bound into published election metadata (Architecture.md § 3.1), so
/// tampering with metadata after deployment is detectable by hash mismatch.
/// </summary>
public sealed class MetadataHasher : IMetadataHasher
{
    /// <summary>Computes the lower-case hexadecimal SHA-256 hash of the given canonical text.</summary>
    public string ComputeSha256Hex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
