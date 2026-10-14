using ChainBallot.PublicContracts;

namespace ElectionProcessing.Api.Processing;

public interface IProjectionEvidencePublisher
{
    Task PublishBatchAnchoredAsync(ProjectionEvidenceBatch batch, CancellationToken cancellationToken);
}

public sealed record ProjectionEvidenceBatch(
    string ElectionId,
    string LifecycleState,
    string ResultDigest,
    SettlementReference SettlementReference,
    IReadOnlyDictionary<string, long> CandidateTallies,
    IReadOnlyList<ProjectionVidEvidence> VidEntries,
    DateTimeOffset GeneratedAtUtc,
    string? MetadataDigest = null,
    string? AuditDigest = null,
    bool IsStale = false,
    string? StaleReason = null);

public sealed record ProjectionVidEvidence(
    string Vid,
    string BatchId,
    string BallotCommitment,
    InclusionProofReference InclusionProofReference,
    DateTimeOffset RecordedAtUtc,
    string? ConsistencyDigest);
