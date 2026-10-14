using ChainBallot.PublicContracts;

namespace TransparencyAudit.Api.Endpoints;

public sealed record ProjectionIngestionRequest(
    string LifecycleState,
    string ResultDigest,
    SettlementReference SettlementReference,
    IReadOnlyDictionary<string, long> CandidateTallies,
    IReadOnlyList<VidEvidenceRequest> VidEntries,
    string? MetadataDigest,
    string? AuditDigest,
    bool IsStale,
    string? StaleReason);

public sealed record VidEvidenceRequest(
    string Vid,
    string BatchId,
    string BallotCommitment,
    InclusionProofReference InclusionProofReference,
    DateTimeOffset RecordedAtUtc,
    string? ConsistencyDigest);

public sealed record StaleProjectionRequest(string Reason);

public sealed record ProjectionStatusResponse(
    string ElectionId,
    string LifecycleState,
    bool IsStale,
    string? StaleReason,
    string? ConsistencyDigest,
    string? MetadataDigest,
    string? AuditDigest,
    int VidCount,
    DateTimeOffset GeneratedAtUtc);

public sealed record CanonicalResyncTriggerRequest(
    string ElectionId,
    string Reason,
    string Source,
    string? CorrelationId);

public sealed record ReconciliationDisputeRequest(
    string ElectionId,
    string Reason,
    string Actor,
    string? ExternalEvidenceDigest,
    string? Notes);

public sealed record ReconciliationOverrideRequest(
    string Actor,
    string Resolution,
    string Reason,
    string? ExternalEvidenceDigest,
    string? Notes);

public sealed record ReconciliationResolveRequest(
    string Actor,
    string Outcome,
    string Reason,
    string? Notes);

public sealed record ReconciliationEventResponse(
    string EventType,
    string Actor,
    string Reason,
    string? Notes,
    string? ExternalEvidenceDigest,
    DateTimeOffset OccurredAtUtc);

public sealed record ReconciliationCaseResponse(
    string CaseId,
    string ElectionId,
    string Status,
    string CurrentDigest,
    string? ExternalEvidenceDigest,
    string OpenedBy,
    string OpenReason,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ReconciliationEventResponse> Events);
