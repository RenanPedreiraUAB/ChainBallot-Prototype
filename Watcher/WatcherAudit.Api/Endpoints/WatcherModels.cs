using WatcherAudit.Api.Domain;

namespace WatcherAudit.Api.Endpoints;

public sealed record RunAuditRequest(string? RequestedBy);

public sealed record WatcherCheckResponse(
    string Name,
    bool Passed,
    string Severity,
    string Message,
    string? Observed,
    string? Expected);

public sealed record WatcherEvidenceResponse(
    string? SnapshotLifecycleState,
    string? SnapshotResultDigest,
    string? SnapshotTransactionSignature,
    string? ProjectionConsistencyDigest,
    string? ProjectionMetadataDigest,
    string? ProjectionAuditDigest,
    bool? ProjectionIsStale,
    string? ProjectionStaleReason,
    bool? CanonicalIsFinalized,
    string? CanonicalSource,
    string? AuthoritativeMetadataDigest,
    string? AuthoritativeAuditDigest);

public sealed record WatcherReportResponse(
    string ReportId,
    string ElectionId,
    string Status,
    DateTimeOffset GeneratedAtUtc,
    string CorrelationId,
    string Summary,
    IReadOnlyList<WatcherCheckResponse> Checks,
    WatcherEvidenceResponse Evidence,
    string ReportDigest,
    string Signature);

public sealed record OperatorElectionSummaryResponse(
    string ElectionId,
    int TotalReports,
    int ConsistentCount,
    int InconsistentCount,
    int ErrorCount,
    WatcherReportResponse? LatestReport);

public static class WatcherModelMapper
{
    public static WatcherReportResponse Map(WatcherAuditReport source)
        => new(
            source.ReportId,
            source.ElectionId,
            source.Status.ToString(),
            source.GeneratedAtUtc,
            source.CorrelationId,
            source.Summary,
            source.Checks.Select(c => new WatcherCheckResponse(
                c.Name,
                c.Passed,
                c.Severity.ToString(),
                c.Message,
                c.Observed,
                c.Expected)).ToArray(),
            new WatcherEvidenceResponse(
                source.Evidence.SnapshotLifecycleState,
                source.Evidence.SnapshotResultDigest,
                source.Evidence.SnapshotSettlementReference?.TransactionSignature,
                source.Evidence.ProjectionConsistencyDigest,
                source.Evidence.ProjectionMetadataDigest,
                source.Evidence.ProjectionAuditDigest,
                source.Evidence.ProjectionIsStale,
                source.Evidence.ProjectionStaleReason,
                source.Evidence.CanonicalIsFinalized,
                source.Evidence.CanonicalSource,
                source.Evidence.AuthoritativeMetadataDigest,
                source.Evidence.AuthoritativeAuditDigest),
            source.ReportDigest,
            source.Signature);
}
