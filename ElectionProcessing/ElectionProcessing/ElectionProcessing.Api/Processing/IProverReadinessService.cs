namespace ElectionProcessing.Api.Processing;

public interface IProverReadinessService
{
    Task<ProverReadinessSnapshot> GetReadinessAsync(CancellationToken cancellationToken);
}

public sealed record ProverReadinessSnapshot(
    string Mode,
    bool IsReady,
    string? Reason,
    DateTimeOffset CheckedAtUtc);
