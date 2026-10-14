using ChainBallot.PublicContracts;

namespace ElectionProcessing.Api.Processing;

public interface IBallotQueueConsumer
{
    int ApproximateDepth { get; }

    Task<IReadOnlyList<BallotSubmissionPackage>> DrainAsync(CancellationToken cancellationToken);
}
