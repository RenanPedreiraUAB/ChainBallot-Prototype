using ConfigurationComponent.Domain;

namespace ConfigurationComponent.Services;

/// <summary>
/// Defines metadata generation and serialization logic for elections.
/// </summary>
public interface IMetadataGenerator
{
    /// <summary>
    /// Builds election metadata using deployment context and verification fields.
    /// </summary>
    /// <param name="election">Election source data.</param>
    /// <param name="electionPda">Derived election PDA reference.</param>
    /// <param name="metadataHash">Canonical metadata hash value.</param>
    /// <param name="creationTimestamp">Optional fixed creation timestamp for deterministic output.</param>
    /// <returns>Structured election metadata object.</returns>
    ElectionMetadata BuildMetadata(
        ElectionConfiguration election,
        string electionPda,
        string metadataHash,
        long? creationTimestamp = null);

    /// <summary>
    /// Serializes election metadata to JSON.
    /// </summary>
    /// <param name="metadata">Metadata document to serialize.</param>
    /// <returns>Serialized metadata JSON.</returns>
    string Serialize(ElectionMetadata metadata);
}
