namespace TransparencyAudit.Api.Services;

public sealed record DigestCorrelationOutcome(
    string Domain,
    bool IsConsistent,
    string? ExpectedDigest,
    string? ProvidedDigest,
    string? EffectiveDigest,
    string? Reason);
