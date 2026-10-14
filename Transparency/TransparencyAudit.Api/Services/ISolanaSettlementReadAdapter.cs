namespace TransparencyAudit.Api.Services;

public interface ISolanaSettlementReadAdapter
{
    Task<CanonicalElectionState?> GetElectionStateAsync(string electionId, CancellationToken cancellationToken);
}

public sealed record CanonicalElectionState(
    string ElectionId,
    bool IsFinalized,
    string Source,
    DateTimeOffset CheckedAtUtc,
    string? Note);
