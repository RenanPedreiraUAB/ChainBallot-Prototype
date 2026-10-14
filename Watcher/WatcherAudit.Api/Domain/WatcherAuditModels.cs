using ChainBallot.PublicContracts;

namespace WatcherAudit.Api.Domain;

public enum WatcherReportStatus
{
    Consistent = 0,
    Inconsistent = 1,
    Error = 2
}

public enum WatcherCheckSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public sealed record WatcherCheckResult(
    string Name,
    bool Passed,
    WatcherCheckSeverity Severity,
    string Message,
    string? Observed,
    string? Expected);

public sealed record WatcherEvidence(
    string? SnapshotLifecycleState,
    string? SnapshotResultDigest,
    SettlementReference? SnapshotSettlementReference,
    string? ProjectionConsistencyDigest,
    string? ProjectionMetadataDigest,
    string? ProjectionAuditDigest,
    bool? ProjectionIsStale,
    string? ProjectionStaleReason,
    bool? CanonicalIsFinalized,
    string? CanonicalSource,
    string? CanonicalNote,
    string? AuthoritativeMetadataDigest,
    string? AuthoritativeAuditDigest);

public sealed record WatcherAuditReport(
    string ReportId,
    string ElectionId,
    WatcherReportStatus Status,
    DateTimeOffset GeneratedAtUtc,
    string CorrelationId,
    string Summary,
    IReadOnlyList<WatcherCheckResult> Checks,
    WatcherEvidence Evidence,
    string ReportDigest,
    string Signature);
