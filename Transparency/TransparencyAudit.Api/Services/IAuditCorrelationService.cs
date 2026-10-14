namespace TransparencyAudit.Api.Services;

/// <summary>Correlates a projection's audit digest against the authoritative audit digest source (Architecture.md § 3.8).</summary>
public interface IAuditCorrelationService
{
    /// <summary>Compares the provided audit digest to the authoritative value for an election.</summary>
    Task<DigestCorrelationOutcome> CorrelateAsync(string electionId, string? providedDigest, CancellationToken cancellationToken);
}
