using TransparencyAudit.Api.Domain;

namespace TransparencyAudit.Api.Services;

/// <summary>
/// Write-side boundary of the Transparency &amp; Audit Component's read model (Architecture.md § 3.8).
/// Accepts settlement/finalization evidence from the Election Processing and Threshold Decryption
/// components, and provides drift-recovery operations (reindex, mark-stale) so the projection never
/// silently diverges from canonical settlement state.
/// </summary>
public interface IProjectionManagementService
{
    /// <summary>Ingests anchored-batch or finalization evidence into the election's projection.</summary>
    Task<ElectionProjection> IngestEvidenceAsync(ProjectionEvidenceIngestion evidence, CancellationToken cancellationToken);

    /// <summary>Rebuilds an election's projection from canonical/authoritative sources on demand.</summary>
    Task<ElectionProjection?> ReindexElectionAsync(string electionId, CancellationToken cancellationToken);

    /// <summary>Marks an election's projection stale (e.g. on authoritative digest mismatch) pending reconciliation.</summary>
    Task<ElectionProjection?> MarkStaleAsync(string electionId, string staleReason, CancellationToken cancellationToken);
}
