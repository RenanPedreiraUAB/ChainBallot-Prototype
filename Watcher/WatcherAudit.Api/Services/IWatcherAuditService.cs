using WatcherAudit.Api.Domain;

namespace WatcherAudit.Api.Services;

/// <summary>
/// Independent-audit boundary for the Watcher Component (see Architecture.md § 3.10).
/// Cross-validates the Transparency &amp; Audit Component's read model, the canonical settlement
/// ledger state and an authoritative metadata/audit digest source, producing one signed report
/// per audit run.
/// </summary>
public interface IWatcherAuditService
{
    /// <summary>
    /// Runs a full consistency audit for one election: fetches the verified snapshot, projection
    /// status, canonical settlement state and authoritative digests, evaluates lifecycle/settlement/digest
    /// consistency checks, and returns a signed, persisted <see cref="WatcherAuditReport"/>.
    /// </summary>
    /// <param name="electionId">Election identifier to audit.</param>
    /// <param name="requestedBy">Optional operator identity that requested the run, recorded for traceability.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    Task<WatcherAuditReport> RunAuditAsync(string electionId, string? requestedBy, CancellationToken cancellationToken);
}
