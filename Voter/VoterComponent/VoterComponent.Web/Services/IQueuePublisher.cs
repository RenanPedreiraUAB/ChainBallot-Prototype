using ChainBallot.PublicContracts;
using VoterComponent.Web.Contracts;

namespace VoterComponent.Web.Services;

public interface IQueuePublisher
{
    Task<QueueReceipt> PublishAsync(BallotSubmissionPackage package, CancellationToken cancellationToken);

    IReadOnlyCollection<BallotSubmissionPackage> Snapshot();
}