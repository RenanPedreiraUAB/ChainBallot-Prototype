namespace ThresholdDecryption.Api.Services;

/// <summary>
/// Reads the canonical election lifecycle signal from the Transparency component's public
/// canonical-state endpoint (Architecture.md § 4.2/4.3: "Confirm election closed" precondition).
/// PROTOTYPE SIMPLIFICATION: this is the only network-reachable canonical lifecycle signal exposed by
/// any component today — there is no distinct Closed-vs-Finalized signal anywhere in this system, only
/// Transparency's <see cref="CanonicalElectionStateDto.IsFinalized"/> boolean. That flag is therefore
/// used as the ceremony's Closed-state precondition gate; do not reinterpret it as a general lifecycle
/// enum without first adding a real distinct signal end to end.
/// </summary>
public interface ICanonicalElectionStateClient
{
    Task<CanonicalElectionStateDto?> GetElectionStateAsync(string electionId, CancellationToken cancellationToken);
}

public sealed record CanonicalElectionStateDto(
    string ElectionId,
    bool IsFinalized,
    string Source,
    DateTimeOffset CheckedAtUtc,
    string? Note);
