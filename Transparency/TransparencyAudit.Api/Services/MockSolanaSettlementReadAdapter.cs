namespace TransparencyAudit.Api.Services;

public sealed class MockSolanaSettlementReadAdapter : ISolanaSettlementReadAdapter
{
    private readonly IReadOnlyDictionary<string, bool> _finalizedStates;

    public MockSolanaSettlementReadAdapter(IReadOnlyDictionary<string, bool> finalizedStates)
    {
        _finalizedStates = finalizedStates;
    }

    public Task<CanonicalElectionState?> GetElectionStateAsync(string electionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(electionId))
        {
            return Task.FromResult<CanonicalElectionState?>(null);
        }

        if (!_finalizedStates.TryGetValue(electionId, out var isFinalized))
        {
            return Task.FromResult<CanonicalElectionState?>(null);
        }

        return Task.FromResult<CanonicalElectionState?>(
            new CanonicalElectionState(
                electionId,
                isFinalized,
                "MockSolana",
                DateTimeOffset.UtcNow,
                "Resolved from configured mock canonical state."));
    }
}
