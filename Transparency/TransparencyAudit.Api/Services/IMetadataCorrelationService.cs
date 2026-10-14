namespace TransparencyAudit.Api.Services;

/// <summary>Correlates a projection's metadata digest against the authoritative metadata digest source (Architecture.md § 3.8).</summary>
public interface IMetadataCorrelationService
{
    /// <summary>Compares the provided metadata digest to the authoritative value for an election.</summary>
    Task<DigestCorrelationOutcome> CorrelateAsync(string electionId, string? providedDigest, CancellationToken cancellationToken);
}
