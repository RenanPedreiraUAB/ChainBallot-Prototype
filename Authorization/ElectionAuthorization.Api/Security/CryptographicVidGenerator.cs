using System.Security.Cryptography;

namespace ElectionAuthorization.Api.Security;

/// <summary>
/// Generates opaque, random VIDs for privacy-preserving identity separation.
/// </summary>
public sealed class CryptographicVidGenerator : IVidGenerator
{
    /// <inheritdoc />
    public string GenerateVid()
    {
        var bytes = RandomNumberGenerator.GetBytes(18);
        return "vid_" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
