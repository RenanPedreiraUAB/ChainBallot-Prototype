using ChainBallot.PublicContracts;

namespace ElectionProcessing.Api.Processing;

public interface IBallotQueueIngress
{
    Task EnqueueAsync(BallotSubmissionPackage package, CancellationToken cancellationToken);
}
