namespace ChainBallot.PublicContracts;

public enum VerificationOutcome
{
    Verified = 0,
    NotFound = 1,
    Inconsistent = 2,
    ElectionNotFinalized = 3,
    Error = 4
}

public sealed record VidVerificationResponse(
    string ElectionId,
    string Vid,
    VerificationOutcome Outcome,
    string? Message,
    SettlementReference? SettlementReference,
    InclusionProofReference? InclusionProofReference,
    DateTimeOffset CheckedAtUtc,
    string? ConsistencyDigest);
