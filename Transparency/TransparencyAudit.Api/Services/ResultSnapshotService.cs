using ChainBallot.PublicContracts;

namespace TransparencyAudit.Api.Services;

/// <summary>
/// Verified result snapshot (Architecture.md § 3.8). Fail-closed: a stale projection, or one whose
/// finalisation cannot be corroborated against a configured canonical settlement read source, is never
/// surfaced as a verified snapshot (returns null, mapped to 404 by the endpoint).
/// </summary>
public sealed class ResultSnapshotService : IResultSnapshotService
{
    private const string FinalizedState = "Finalized";
    private readonly IProjectionRepository _projectionRepository;
    private readonly ISolanaSettlementReadAdapter _solanaSettlementReadAdapter;

    public ResultSnapshotService(IProjectionRepository projectionRepository, ISolanaSettlementReadAdapter solanaSettlementReadAdapter)
    {
        _projectionRepository = projectionRepository;
        _solanaSettlementReadAdapter = solanaSettlementReadAdapter;
    }

    public async Task<VerifiedResultSnapshot?> GetSnapshotAsync(string electionId, CancellationToken cancellationToken)
    {
        var projection = await _projectionRepository.GetElectionProjectionAsync(electionId, cancellationToken).ConfigureAwait(false);
        if (projection is null)
        {
            return null;
        }

        if (projection.IsStale)
        {
            return null;
        }

        if (string.Equals(projection.LifecycleState, FinalizedState, StringComparison.OrdinalIgnoreCase))
        {
            var canonicalState = await _solanaSettlementReadAdapter.GetElectionStateAsync(electionId, cancellationToken).ConfigureAwait(false);
            if (canonicalState is not null && !canonicalState.IsFinalized)
            {
                return null;
            }
        }

        return new VerifiedResultSnapshot(
            projection.ElectionId,
            projection.ResultDigest,
            projection.LifecycleState,
            projection.SettlementReference,
            projection.CandidateTallies,
            projection.GeneratedAtUtc);
    }
}
