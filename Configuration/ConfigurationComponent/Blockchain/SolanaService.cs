using ConfigurationComponent.Domain;

namespace ConfigurationComponent.Blockchain;

/// <summary>
/// Represents Solana Service.
/// </summary>
public sealed class SolanaService : ISolanaService
{
    private readonly Dictionary<Guid, ElectionLifecycleState> _states = [];

    /// <summary>
    /// Executes Deploy Election Async.
    /// </summary>
    public async Task<DeploymentResult> DeployElectionAsync(
        ElectionConfiguration election,
        string metadataHash,
        string metadataUri,
        CancellationToken cancellationToken = default)
    {
        await InitializeElectionAsync(election, metadataHash, cancellationToken);

        var pda = $"pda_{election.Id:N}";
        var signature = $"tx_{Guid.NewGuid():N}";

        return new DeploymentResult(
            ProgramId: election.DeploymentSettings.ProgramId,
            ElectionPda: pda,
            MetadataUri: metadataUri,
            TransactionSignature: signature,
            MetadataHash: metadataHash);
    }

    /// <summary>
    /// Executes Initialize Election Async.
    /// </summary>
    public async Task InitializeElectionAsync(ElectionConfiguration election, string metadataHash, CancellationToken cancellationToken = default)
    {
        _ = metadataHash;

        await Task.Delay(300, cancellationToken);
        _states[election.Id] = ElectionLifecycleState.Deployed;
    }

    /// <summary>
    /// Executes Activate Election Async.
    /// </summary>
    public Task ActivateElectionAsync(Guid electionId, CancellationToken cancellationToken = default)
    {
        _states[electionId] = ElectionLifecycleState.Open;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes Close Election Async.
    /// </summary>
    public Task CloseElectionAsync(Guid electionId, CancellationToken cancellationToken = default)
    {
        _states[electionId] = ElectionLifecycleState.Closed;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes Get Election Status Async.
    /// </summary>
    public Task<ElectionLifecycleState> GetElectionStatusAsync(Guid electionId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var state = _states.TryGetValue(electionId, out var found) ? found : ElectionLifecycleState.Draft;
        return Task.FromResult(state);
    }
}
