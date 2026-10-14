namespace ConfigurationComponent.Services;

/// <summary>
/// Defines cryptographic hash generation for metadata payloads.
/// </summary>
public interface IMetadataHasher
{
    /// <summary>
    /// Computes a SHA-256 hash and returns it as a lowercase hexadecimal string.
    /// </summary>
    /// <param name="text">Input text to hash.</param>
    /// <returns>SHA-256 hexadecimal digest.</returns>
    string ComputeSha256Hex(string text);
}
