using ChainBallot.PublicContracts;

namespace TransparencyAudit.Api.Domain;

public sealed record VidEntry(
    string ElectionId,
    string Vid,
    string BatchId,
    string BallotCommitment,
    SettlementReference SettlementReference,
    InclusionProofReference InclusionProofReference,
    DateTimeOffset RecordedAtUtc,
    string? ConsistencyDigest);

public sealed record ElectionProjection(
    string ElectionId,
    string LifecycleState,
    SettlementReference SettlementReference,
    IReadOnlyDictionary<string, long> CandidateTallies,
    string ResultDigest,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<VidEntry> VidEntries,
    string? ConsistencyDigest = null,
    string? MetadataDigest = null,
    string? AuditDigest = null,
    bool IsStale = false,
    string? StaleReason = null);

public sealed record ProjectionEvidenceIngestion(
    string ElectionId,
    string LifecycleState,
    string ResultDigest,
    SettlementReference SettlementReference,
    IReadOnlyDictionary<string, long> CandidateTallies,
    IReadOnlyList<VidEvidenceEntry> VidEntries,
    string? MetadataDigest,
    string? AuditDigest,
    bool IsStale,
    string? StaleReason);

public sealed record VidEvidenceEntry(
    string Vid,
    string BatchId,
    string BallotCommitment,
    InclusionProofReference InclusionProofReference,
    DateTimeOffset RecordedAtUtc,
    string? ConsistencyDigest);
