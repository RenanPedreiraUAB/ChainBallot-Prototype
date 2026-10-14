using TransparencyAudit.Api.Domain;

namespace TransparencyAudit.Api.Services;

/// <summary>
/// Persistence boundary for the Transparency &amp; Audit Component's read-model projections
/// (Architecture.md § 3.8). The default implementation is file-backed (see <c>FileBackedProjectionRepository</c>).
/// </summary>
public interface IProjectionRepository
{
    /// <summary>Retrieves the current projection for an election, if one exists.</summary>
    Task<ElectionProjection?> GetElectionProjectionAsync(string electionId, CancellationToken cancellationToken);

    /// <summary>Retrieves a single VID's inclusion/verification entry within an election's projection.</summary>
    Task<VidEntry?> GetVidEntryAsync(string electionId, string vid, CancellationToken cancellationToken);

    /// <summary>Applies newly ingested evidence (batch anchoring or finalization) to an election's projection.</summary>
    Task<ElectionProjection> UpsertProjectionEvidenceAsync(ProjectionEvidenceIngestion evidence, CancellationToken cancellationToken);

    /// <summary>Marks an election's projection stale pending reconciliation.</summary>
    Task<ElectionProjection?> MarkElectionStaleAsync(string electionId, string staleReason, CancellationToken cancellationToken);

    /// <summary>Rebuilds an election's projection from canonical/authoritative sources.</summary>
    Task<ElectionProjection?> ReindexElectionAsync(string electionId, CancellationToken cancellationToken);
}
