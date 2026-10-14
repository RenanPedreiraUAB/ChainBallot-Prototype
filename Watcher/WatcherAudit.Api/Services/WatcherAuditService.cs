using WatcherAudit.Api.Domain;

namespace WatcherAudit.Api.Services;

/// <summary>
/// Default implementation of <see cref="IWatcherAuditService"/>. Fans out to the Transparency &amp;
/// Audit Component and to the authoritative digest source in parallel, then correlates the results
/// into a single structured, digested and signed <see cref="WatcherAuditReport"/> (Architecture.md § 3.10).
/// </summary>
public sealed class WatcherAuditService : IWatcherAuditService
{
    private readonly ITransparencyAuditReadClient _transparencyReadClient;
    private readonly IAuthoritativeDigestSource _authoritativeDigestSource;
    private readonly IWatcherReportRepository _reportRepository;
    private readonly IWatcherDigestCalculator _digestCalculator;
    private readonly IWatcherReportSigner _reportSigner;
    private readonly ILogger<WatcherAuditService> _logger;

    public WatcherAuditService(
        ITransparencyAuditReadClient transparencyReadClient,
        IAuthoritativeDigestSource authoritativeDigestSource,
        IWatcherReportRepository reportRepository,
        IWatcherDigestCalculator digestCalculator,
        IWatcherReportSigner reportSigner,
        ILogger<WatcherAuditService> logger)
    {
        _transparencyReadClient = transparencyReadClient;
        _authoritativeDigestSource = authoritativeDigestSource;
        _reportRepository = reportRepository;
        _digestCalculator = digestCalculator;
        _reportSigner = reportSigner;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WatcherAuditReport> RunAuditAsync(string electionId, string? requestedBy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(electionId))
        {
            throw new ArgumentException("Election id is required.", nameof(electionId));
        }

        var correlationId = Guid.NewGuid().ToString("N");
        try
        {
            var snapshotTask = _transparencyReadClient.GetSnapshotAsync(electionId, cancellationToken);
            var projectionTask = _transparencyReadClient.GetProjectionStatusAsync(electionId, cancellationToken);
            var canonicalTask = _transparencyReadClient.GetCanonicalStateAsync(electionId, cancellationToken);
            var digestsTask = _authoritativeDigestSource.GetDigestsAsync(electionId, cancellationToken);

            await Task.WhenAll(snapshotTask, projectionTask, canonicalTask, digestsTask).ConfigureAwait(false);

            var snapshot = snapshotTask.Result;
            var projection = projectionTask.Result;
            var canonical = canonicalTask.Result;
            var digests = digestsTask.Result;

            var checks = BuildChecks(electionId, snapshot, projection, canonical, digests);
            var status = ResolveStatus(checks);
            var summary = BuildSummary(status, checks);

            var evidence = new WatcherEvidence(
                snapshot.Value?.LifecycleState,
                snapshot.Value?.ResultDigest,
                snapshot.Value?.SettlementReference,
                projection.Value?.ConsistencyDigest,
                projection.Value?.MetadataDigest,
                projection.Value?.AuditDigest,
                projection.Value?.IsStale,
                projection.Value?.StaleReason,
                canonical.Value?.IsFinalized,
                canonical.Value?.Source,
                canonical.Value?.Note,
                digests.MetadataDigest,
                digests.AuditDigest);

            var unsignedReport = new WatcherAuditReport(
                Guid.NewGuid().ToString("N"),
                electionId,
                status,
                DateTimeOffset.UtcNow,
                correlationId,
                summary,
                checks,
                evidence,
                string.Empty,
                string.Empty);

            var digest = _digestCalculator.ComputeDigest(unsignedReport);
            var signature = _reportSigner.Sign(digest);
            var finalizedReport = unsignedReport with { ReportDigest = digest, Signature = signature };

            return await _reportRepository.SaveAsync(finalizedReport, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Watcher audit run failed. ElectionId={ElectionId} CorrelationId={CorrelationId} RequestedBy={RequestedBy}", electionId, correlationId, requestedBy ?? "anonymous");

            var errorReport = new WatcherAuditReport(
                Guid.NewGuid().ToString("N"),
                electionId,
                WatcherReportStatus.Error,
                DateTimeOffset.UtcNow,
                correlationId,
                "Watcher audit run failed unexpectedly.",
                [new WatcherCheckResult("audit-execution", false, WatcherCheckSeverity.Error, ex.Message, ex.GetType().Name, "no exception")],
                new WatcherEvidence(null, null, null, null, null, null, null, null, null, null, null, null, null),
                string.Empty,
                string.Empty);

            var digest = _digestCalculator.ComputeDigest(errorReport);
            var signature = _reportSigner.Sign(digest);
            var finalizedReport = errorReport with { ReportDigest = digest, Signature = signature };
            return await _reportRepository.SaveAsync(finalizedReport, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<WatcherCheckResult> BuildChecks(
        string electionId,
        UpstreamReadResult<ChainBallot.PublicContracts.VerifiedResultSnapshot> snapshot,
        UpstreamReadResult<ProjectionStatusDto> projection,
        UpstreamReadResult<CanonicalElectionStateDto> canonical,
        AuthoritativeDigestPair digests)
    {
        var checks = new List<WatcherCheckResult>();

        checks.Add(CreatePresenceCheck("snapshot-availability", snapshot, required: true));
        checks.Add(CreatePresenceCheck("projection-status-availability", projection, required: true));
        checks.Add(CreatePresenceCheck("canonical-state-availability", canonical, required: true));

        if (snapshot.Status == UpstreamReadStatus.Success && snapshot.Value is not null)
        {
            checks.Add(new WatcherCheckResult(
                "snapshot-election-id",
                string.Equals(snapshot.Value.ElectionId, electionId, StringComparison.OrdinalIgnoreCase),
                WatcherCheckSeverity.Error,
                "Snapshot election id must match requested election id.",
                snapshot.Value.ElectionId,
                electionId));

            checks.Add(new WatcherCheckResult(
                "snapshot-result-digest-present",
                !string.IsNullOrWhiteSpace(snapshot.Value.ResultDigest),
                WatcherCheckSeverity.Error,
                "Snapshot result digest must be present.",
                snapshot.Value.ResultDigest,
                "non-empty"));
        }

        if (projection.Status == UpstreamReadStatus.Success && projection.Value is not null)
        {
            checks.Add(new WatcherCheckResult(
                "projection-not-stale",
                !projection.Value.IsStale,
                WatcherCheckSeverity.Error,
                "Projection must not be stale for a consistent report.",
                projection.Value.StaleReason,
                "not stale"));

            checks.Add(new WatcherCheckResult(
                "projection-election-id",
                string.Equals(projection.Value.ElectionId, electionId, StringComparison.OrdinalIgnoreCase),
                WatcherCheckSeverity.Error,
                "Projection election id must match requested election id.",
                projection.Value.ElectionId,
                electionId));
        }

        if (snapshot.Status == UpstreamReadStatus.Success && canonical.Status == UpstreamReadStatus.Success &&
            snapshot.Value is not null && canonical.Value is not null)
        {
            var expectsFinalized = string.Equals(snapshot.Value.LifecycleState, "Finalized", StringComparison.OrdinalIgnoreCase);
            checks.Add(new WatcherCheckResult(
                "lifecycle-vs-canonical-finalization",
                expectsFinalized == canonical.Value.IsFinalized,
                WatcherCheckSeverity.Error,
                "Snapshot lifecycle must agree with canonical finalization state.",
                $"lifecycle={snapshot.Value.LifecycleState};canonical={canonical.Value.IsFinalized}",
                "consistent finalization"));
        }

        if (projection.Status == UpstreamReadStatus.Success && projection.Value is not null)
        {
            if (!string.IsNullOrWhiteSpace(digests.MetadataDigest))
            {
                checks.Add(new WatcherCheckResult(
                    "metadata-digest-match",
                    string.Equals(projection.Value.MetadataDigest, digests.MetadataDigest, StringComparison.Ordinal),
                    WatcherCheckSeverity.Error,
                    "Projection metadata digest must match authoritative metadata digest.",
                    projection.Value.MetadataDigest,
                    digests.MetadataDigest));
            }

            if (!string.IsNullOrWhiteSpace(digests.AuditDigest))
            {
                checks.Add(new WatcherCheckResult(
                    "audit-digest-match",
                    string.Equals(projection.Value.AuditDigest, digests.AuditDigest, StringComparison.Ordinal),
                    WatcherCheckSeverity.Error,
                    "Projection audit digest must match authoritative audit digest.",
                    projection.Value.AuditDigest,
                    digests.AuditDigest));
            }
        }

        return checks;
    }

    private static WatcherCheckResult CreatePresenceCheck<T>(string name, UpstreamReadResult<T> result, bool required)
    {
        var passed = result.Status == UpstreamReadStatus.Success && result.Value is not null;
        var severity = required ? WatcherCheckSeverity.Error : WatcherCheckSeverity.Warning;
        var message = passed
            ? "Required upstream evidence is available."
            : "Required upstream evidence is missing or unavailable.";

        return new WatcherCheckResult(
            name,
            passed,
            severity,
            message,
            result.Status.ToString(),
            UpstreamReadStatus.Success.ToString());
    }

    private static WatcherReportStatus ResolveStatus(IReadOnlyList<WatcherCheckResult> checks)
    {
        return checks.All(c => c.Passed)
            ? WatcherReportStatus.Consistent
            : WatcherReportStatus.Inconsistent;
    }

    private static string BuildSummary(WatcherReportStatus status, IReadOnlyList<WatcherCheckResult> checks)
    {
        var failed = checks.Count(c => !c.Passed);
        return status switch
        {
            WatcherReportStatus.Consistent => "No inconsistency detected across transparency, canonical state, and authoritative digests.",
            WatcherReportStatus.Inconsistent => $"Detected {failed} inconsistent check(s).",
            _ => "Audit status unresolved."
        };
    }
}
