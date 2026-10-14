namespace VoterComponent.Web.Contracts;

public sealed record BallotSelectionRequest(
    string ElectionId,
    int CandidateId,
    string CandidateName);

public sealed record BallotSubmissionDraft(
    string ElectionId,
    string Subject,
    string Vid,
    int CandidateId,
    string CandidateName,
    string MetadataHash,
    string SubmissionNonce,
    string BallotNullifier,
    DateTimeOffset SubmittedAtUtc);