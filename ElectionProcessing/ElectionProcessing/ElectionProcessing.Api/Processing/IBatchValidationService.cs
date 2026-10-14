using ChainBallot.PublicContracts;

namespace ElectionProcessing.Api.Processing;

public interface IBatchValidationService
{
    BatchValidationResult Validate(string electionId, IReadOnlyList<BallotSubmissionPackage> ballots, BatchValidationContext context);
}

public sealed record BatchValidationContext(
    IReadOnlySet<string> SeenNullifiers,
    IReadOnlySet<string> SeenCommitments);

public sealed record BatchValidationResult(
    IReadOnlyList<BallotSubmissionPackage> Accepted,
    IReadOnlyList<RejectedBallot> Rejected);

public sealed record RejectedBallot(
    BallotSubmissionPackage Ballot,
    string Reason);
