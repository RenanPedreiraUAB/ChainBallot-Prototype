namespace ChainBallot.PublicContracts;

public sealed record BatchAnchorReference(
    string ElectionId,
    string BatchId,
    int BatchSequence,
    int BallotCount,
    string BatchRoot,
    string? ProofSystem,
    string? ProofReference,
    SettlementReference SettlementReference,
    DateTimeOffset AnchoredAtUtc);

public sealed record VerifiedResultSnapshot(
    string ElectionId,
    string ResultDigest,
    string LifecycleState,
    SettlementReference SettlementReference,
    IReadOnlyDictionary<string, long> CandidateTallies,
    DateTimeOffset GeneratedAtUtc);
