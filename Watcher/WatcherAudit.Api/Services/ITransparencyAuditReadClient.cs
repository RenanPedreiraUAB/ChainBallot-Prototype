using ChainBallot.PublicContracts;
using WatcherAudit.Api.Domain;

namespace WatcherAudit.Api.Services;

/// <summary>
/// Watcher's read client toward the Transparency &amp; Audit Component (Architecture.md § 3.10) —
/// the same read model consumed by the Result Portal, so the Watcher's checks are meaningful
/// cross-validations rather than a second, independent data source.
/// </summary>
public interface ITransparencyAuditReadClient
{
    /// <summary>Requests the verified result snapshot for an election.</summary>
    Task<UpstreamReadResult<VerifiedResultSnapshot>> GetSnapshotAsync(string electionId, CancellationToken cancellationToken);

    /// <summary>Requests the projection status (consistency digest, staleness) for an election.</summary>
    Task<UpstreamReadResult<ProjectionStatusDto>> GetProjectionStatusAsync(string electionId, CancellationToken cancellationToken);

    /// <summary>Requests the canonical settlement-ledger state for an election.</summary>
    Task<UpstreamReadResult<CanonicalElectionStateDto>> GetCanonicalStateAsync(string electionId, CancellationToken cancellationToken);
}
