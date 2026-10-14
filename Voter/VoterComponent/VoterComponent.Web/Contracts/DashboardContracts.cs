using ChainBallot.PublicContracts;

namespace VoterComponent.Web.Contracts;

public sealed record VoterSession(
    string Subject,
    string Username,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);

public sealed record CandidateOption(
    int Id,
    string Name,
    string Party,
    string Description);

public sealed record VoterDashboardState(
    string ElectionId,
    VoterAuthorizationProfile? Profile,
    ElectionAuthorizationState? AuthorizationState,
    ElectionMetadataDocument? Metadata,
    IReadOnlyList<CandidateOption> CandidateOptions,
    string? StatusMessage,
    bool CanVote,
    string? SelectedCandidateName,
    int? SelectedCandidateId,
    string? MetadataEndpoint,
    QueueReceipt? LastQueueReceipt);

public sealed record VoterSubmissionOutcome(
    bool Succeeded,
    string Message,
    AuthorizationGrant? Grant,
    ConsumeAuthorizationResponse? ConsumeResponse,
    QueueReceipt? QueueReceipt,
    BallotSubmissionPackage? BallotPackage);