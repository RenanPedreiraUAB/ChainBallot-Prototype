namespace TransparencyAudit.Api.Services;

public sealed class ProjectionOnlySolanaSettlementReadAdapter : ISolanaSettlementReadAdapter
{
    public Task<CanonicalElectionState?> GetElectionStateAsync(string electionId, CancellationToken cancellationToken)
        => Task.FromResult<CanonicalElectionState?>(null);
}
