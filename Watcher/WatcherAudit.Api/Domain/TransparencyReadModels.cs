using ChainBallot.PublicContracts;

namespace WatcherAudit.Api.Domain;

public enum UpstreamReadStatus
{
    Success = 0,
    NotFound = 1,
    Unauthorized = 2,
    Unavailable = 3,
    Error = 4
}

public sealed record UpstreamReadResult<T>(
    UpstreamReadStatus Status,
    T? Value,
    string? Message);

public sealed record ProjectionStatusDto(
    string ElectionId,
    string LifecycleState,
    bool IsStale,
    string? StaleReason,
    string? ConsistencyDigest,
    string? MetadataDigest,
    string? AuditDigest,
    int VidCount,
    DateTimeOffset GeneratedAtUtc);

public sealed record CanonicalElectionStateDto(
    string ElectionId,
    bool IsFinalized,
    string Source,
    DateTimeOffset CheckedAtUtc,
    string? Note);

public sealed record TransparencySnapshotBundle(
    VerifiedResultSnapshot? Snapshot,
    ProjectionStatusDto? ProjectionStatus,
    CanonicalElectionStateDto? CanonicalState);
