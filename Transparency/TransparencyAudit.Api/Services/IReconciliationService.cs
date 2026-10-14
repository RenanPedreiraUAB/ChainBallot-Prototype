namespace TransparencyAudit.Api.Services;

/// <summary>
/// Formal reconciliation workflow for the Transparency &amp; Audit Component (Architecture.md § 3.8 /
/// § 5.3): disputes, override requests and resolutions are recorded as an immutable case-event trail,
/// so disagreements between operators/auditors are auditable rather than silently overwritten.
/// </summary>
public interface IReconciliationService
{
    /// <summary>Opens a new reconciliation dispute for an election.</summary>
    Task<ReconciliationCase> OpenDisputeAsync(
        string electionId,
        string actor,
        string reason,
        string? notes,
        string? externalEvidenceDigest,
        CancellationToken cancellationToken);

    /// <summary>Requests an override resolution for an open dispute case.</summary>
    Task<ReconciliationCase?> RequestOverrideAsync(
        string caseId,
        string actor,
        string reason,
        string resolution,
        string? notes,
        string? externalEvidenceDigest,
        CancellationToken cancellationToken);

    Task<ReconciliationCase?> ResolveAsync(
        string caseId,
        string actor,
        string reason,
        string outcome,
        string? notes,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReconciliationCase>> ListByElectionAsync(string electionId, CancellationToken cancellationToken);

    Task<ReconciliationCase?> GetByIdAsync(string caseId, CancellationToken cancellationToken);
}

public sealed record ReconciliationCase(
    string CaseId,
    string ElectionId,
    string Status,
    string CurrentDigest,
    string? ExternalEvidenceDigest,
    string OpenedBy,
    string OpenReason,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ReconciliationCaseEvent> Events);

public sealed record ReconciliationCaseEvent(
    string EventType,
    string Actor,
    string Reason,
    string? Notes,
    string? ExternalEvidenceDigest,
    DateTimeOffset OccurredAtUtc);
