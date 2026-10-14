using ThresholdDecryption.Api.Domain;

namespace ThresholdDecryption.Api.Endpoints;

public sealed record ProposeCeremonyRequest(int? RequiredShares, int? RequiredApprovals, string Actor);

public sealed record ApproveCeremonyRequest(string ApproverId, string Note);

public sealed record SubmitShareRequest(string TrusteeId, string ShareDigest);

public sealed record FinalizeDecryptionRequest(string Actor, IReadOnlyDictionary<string, long> Tallies);

public sealed record PublishFinalTallyRequest(string Actor);

public sealed record ThresholdCeremonyResponse(
    string CeremonyId,
    string ElectionId,
    string Status,
    int RequiredShares,
    int RequiredApprovals,
    int ApprovalCount,
    int ShareCount,
    string? ResultDigest,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? FailureReason,
    string? PublicationReference);

public static class ThresholdMappers
{
    public static ThresholdCeremonyResponse Map(ThresholdCeremony ceremony)
        => new(
            ceremony.CeremonyId,
            ceremony.ElectionId,
            ceremony.Status.ToString(),
            ceremony.RequiredShares,
            ceremony.RequiredApprovals,
            ceremony.Approvals.Count,
            ceremony.Shares.Count,
            ceremony.ResultDigest,
            ceremony.OpenedAtUtc,
            ceremony.CompletedAtUtc,
            ceremony.FailureReason,
            ceremony.Publication?.Reference);
}
