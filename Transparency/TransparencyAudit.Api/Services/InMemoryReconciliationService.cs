using System.Collections.Concurrent;

namespace TransparencyAudit.Api.Services;

/// <summary>
/// In-memory (process-local) implementation of <see cref="IReconciliationService"/>, tracking the
/// dispute/override/resolve case-event trail described in Architecture.md § 3.8/§ 5.3.
/// </summary>
public sealed class InMemoryReconciliationService : IReconciliationService
{
    private readonly IProjectionRepository _projectionRepository;
    private readonly IProjectionManagementService _projectionManagementService;
    private readonly ConcurrentDictionary<string, ReconciliationCase> _casesById = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryReconciliationService(
        IProjectionRepository projectionRepository,
        IProjectionManagementService projectionManagementService)
    {
        _projectionRepository = projectionRepository;
        _projectionManagementService = projectionManagementService;
    }

    public async Task<ReconciliationCase> OpenDisputeAsync(
        string electionId,
        string actor,
        string reason,
        string? notes,
        string? externalEvidenceDigest,
        CancellationToken cancellationToken)
    {
        var projection = await _projectionRepository.GetElectionProjectionAsync(electionId, cancellationToken).ConfigureAwait(false);
        var digest = projection?.ConsistencyDigest ?? string.Empty;
        var now = DateTimeOffset.UtcNow;
        var caseId = Guid.NewGuid().ToString("N");

        var events = new[]
        {
            new ReconciliationCaseEvent("DisputeOpened", actor, reason, notes, externalEvidenceDigest, now)
        };

        var record = new ReconciliationCase(
            caseId,
            electionId,
            "Disputed",
            digest,
            externalEvidenceDigest,
            actor,
            reason,
            now,
            now,
            events);

        _casesById[caseId] = record;
        await _projectionManagementService.MarkStaleAsync(electionId, $"Reconciliation dispute opened: {reason}", cancellationToken).ConfigureAwait(false);
        return record;
    }

    public Task<ReconciliationCase?> RequestOverrideAsync(
        string caseId,
        string actor,
        string reason,
        string resolution,
        string? notes,
        string? externalEvidenceDigest,
        CancellationToken cancellationToken)
    {
        if (!_casesById.TryGetValue(caseId, out var existing))
        {
            return Task.FromResult<ReconciliationCase?>(null);
        }

        var now = DateTimeOffset.UtcNow;
        var nextStatus = string.Equals(resolution, "OverrideAccepted", StringComparison.OrdinalIgnoreCase)
            ? "Overridden"
            : "OverrideRequested";

        var nextEvent = new ReconciliationCaseEvent(
            "OverrideRequested",
            actor,
            reason,
            notes,
            externalEvidenceDigest,
            now);

        var updated = existing with
        {
            Status = nextStatus,
            ExternalEvidenceDigest = externalEvidenceDigest ?? existing.ExternalEvidenceDigest,
            UpdatedAtUtc = now,
            Events = existing.Events.Concat(new[] { nextEvent }).ToArray()
        };

        _casesById[caseId] = updated;
        return Task.FromResult<ReconciliationCase?>(updated);
    }

    public async Task<ReconciliationCase?> ResolveAsync(
        string caseId,
        string actor,
        string reason,
        string outcome,
        string? notes,
        CancellationToken cancellationToken)
    {
        if (!_casesById.TryGetValue(caseId, out var existing))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var finalStatus = string.Equals(outcome, "Rejected", StringComparison.OrdinalIgnoreCase)
            ? "ResolvedRejected"
            : "ResolvedConfirmed";

        var nextEvent = new ReconciliationCaseEvent(
            "Resolved",
            actor,
            reason,
            notes,
            existing.ExternalEvidenceDigest,
            now);

        var updated = existing with
        {
            Status = finalStatus,
            UpdatedAtUtc = now,
            Events = existing.Events.Concat(new[] { nextEvent }).ToArray()
        };

        _casesById[caseId] = updated;

        if (string.Equals(finalStatus, "ResolvedConfirmed", StringComparison.OrdinalIgnoreCase))
        {
            await _projectionManagementService.ReindexElectionAsync(updated.ElectionId, cancellationToken).ConfigureAwait(false);
        }

        return updated;
    }

    public Task<IReadOnlyList<ReconciliationCase>> ListByElectionAsync(string electionId, CancellationToken cancellationToken)
    {
        var items = _casesById.Values
            .Where(x => string.Equals(x.ElectionId, electionId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToArray();

        return Task.FromResult<IReadOnlyList<ReconciliationCase>>(items);
    }

    public Task<ReconciliationCase?> GetByIdAsync(string caseId, CancellationToken cancellationToken)
    {
        _casesById.TryGetValue(caseId, out var item);
        return Task.FromResult(item);
    }
}
