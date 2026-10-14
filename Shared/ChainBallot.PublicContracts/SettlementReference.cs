namespace ChainBallot.PublicContracts;

public sealed record SettlementReference(
    string Network,
    string ProgramId,
    string ElectionId,
    string TransactionSignature,
    ulong Slot,
    DateTimeOffset? BlockTimeUtc,
    string Commitment);
