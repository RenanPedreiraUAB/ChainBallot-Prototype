using ChainBallot.PublicContracts;

namespace TransparencyAudit.Api.Services;

/// <summary>
/// VID verification (Architecture.md § 3.8). Fail-closed: a projection that is stale, or whose
/// finalisation cannot be corroborated against a configured canonical settlement read source, is never
/// reported as <see cref="VerificationOutcome.Verified"/>.
/// </summary>
public sealed class VerificationService : IVerificationService
{
    private const string FinalizedState = "Finalized";
    private readonly IProjectionRepository _projectionRepository;
    private readonly ISolanaSettlementReadAdapter _solanaSettlementReadAdapter;

    public VerificationService(IProjectionRepository projectionRepository, ISolanaSettlementReadAdapter solanaSettlementReadAdapter)
    {
        _projectionRepository = projectionRepository;
        _solanaSettlementReadAdapter = solanaSettlementReadAdapter;
    }

    public async Task<VidVerificationResponse> VerifyVidAsync(string electionId, string vid, CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;

        var projection = await _projectionRepository.GetElectionProjectionAsync(electionId, cancellationToken).ConfigureAwait(false);
        if (projection is null)
        {
            return new VidVerificationResponse(
                electionId,
                vid,
                VerificationOutcome.NotFound,
                "Election projection was not found.",
                null,
                null,
                checkedAt,
                null);
        }

        if (!string.Equals(projection.LifecycleState, FinalizedState, StringComparison.OrdinalIgnoreCase))
        {
            return new VidVerificationResponse(
                electionId,
                vid,
                VerificationOutcome.ElectionNotFinalized,
                "Election is not finalized yet.",
                projection.SettlementReference,
                null,
                checkedAt,
                null);
        }

        var entry = await _projectionRepository.GetVidEntryAsync(electionId, vid, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return new VidVerificationResponse(
                electionId,
                vid,
                VerificationOutcome.NotFound,
                "VID was not found in finalized election projection.",
                projection.SettlementReference,
                null,
                checkedAt,
                null);
        }

        // FAIL-CLOSED: a projection flagged stale by ingestion/reconciliation must never be reported Verified.
        if (projection.IsStale)
        {
            return new VidVerificationResponse(
                electionId,
                vid,
                VerificationOutcome.Inconsistent,
                $"Projection is marked stale: {projection.StaleReason ?? "reason not recorded"}.",
                projection.SettlementReference,
                entry.InclusionProofReference,
                checkedAt,
                entry.ConsistencyDigest);
        }

        // Cross-check canonical settlement state where a canonical read source is actually configured
        // (a null result means no canonical source is configured for this deployment, e.g. ProjectionOnly mode).
        var canonicalState = await _solanaSettlementReadAdapter.GetElectionStateAsync(electionId, cancellationToken).ConfigureAwait(false);
        if (canonicalState is not null && !canonicalState.IsFinalized)
        {
            return new VidVerificationResponse(
                electionId,
                vid,
                VerificationOutcome.Inconsistent,
                "Canonical settlement state does not confirm this election as finalized.",
                projection.SettlementReference,
                entry.InclusionProofReference,
                checkedAt,
                entry.ConsistencyDigest);
        }

        return new VidVerificationResponse(
            electionId,
            vid,
            VerificationOutcome.Verified,
            "VID verification succeeded.",
            entry.SettlementReference,
            entry.InclusionProofReference,
            checkedAt,
            entry.ConsistencyDigest);
    }
}
