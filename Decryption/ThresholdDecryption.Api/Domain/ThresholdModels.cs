namespace ThresholdDecryption.Api.Domain;

public enum CeremonyStatus
{
    Proposed = 0,
    Approved = 1,
    Decrypting = 2,
    Completed = 3,
    Failed = 4
}

public sealed record GovernanceApproval(
    string ApproverId,
    string Note,
    DateTimeOffset ApprovedAtUtc);

public sealed record TrusteeShare(
    string TrusteeId,
    string ShareDigest,
    DateTimeOffset SubmittedAtUtc);

public sealed record DecryptionEvent(
    string EventType,
    string Actor,
    string Message,
    DateTimeOffset OccurredAtUtc);

public sealed record FinalTallyPublication(
    string PublicationId,
    string ElectionId,
    string CeremonyId,
    IReadOnlyDictionary<string, long> Tallies,
    string ResultDigest,
    string Reference,
    DateTimeOffset PublishedAtUtc);

public sealed record ThresholdCeremony(
    string CeremonyId,
    string ElectionId,
    int RequiredShares,
    int RequiredApprovals,
    CeremonyStatus Status,
    IReadOnlyList<GovernanceApproval> Approvals,
    IReadOnlyList<TrusteeShare> Shares,
    IReadOnlyList<DecryptionEvent> Events,
    IReadOnlyDictionary<string, long>? DecryptedTallies,
    string? ResultDigest,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? FailureReason,
    FinalTallyPublication? Publication);
