namespace ChainBallot.PublicContracts;

/// <summary>
/// Encrypted ballot payload container.
/// </summary>
public sealed record EncryptedBallotPayload(
    string Algorithm,
    string NonceBase64,
    string TagBase64,
    string CiphertextBase64);

/// <summary>
/// Cross-component ballot handoff package.
/// Canonical nullifier rule: <c>BallotNullifier = SHA256("{ElectionId}|{Vid}|{SubmissionNonce}")</c>, lower-case hex.
/// </summary>
public sealed record BallotSubmissionPackage(
    string ElectionId,
    string Vid,
    string BallotCommitment,
    string SubmissionNonce,
    string BallotNullifier,
    EncryptedBallotPayload EncryptedVotePayload,
    DateTimeOffset CreatedAtUtc);

public sealed record QueueReceipt(
    Guid QueueItemId,
    DateTimeOffset QueuedAtUtc,
    string QueueMode);