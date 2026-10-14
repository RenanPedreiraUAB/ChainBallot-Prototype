using System.Collections.Concurrent;
using ChainBallot.PublicContracts;

namespace ElectionProcessing.Api.Processing;

public sealed class InMemoryBallotQueueAdapter : IBallotQueueIngress, IBallotQueueConsumer
{
    private readonly ConcurrentQueue<BallotSubmissionPackage> _queue = new();

    public int ApproximateDepth => _queue.Count;

    public Task EnqueueAsync(BallotSubmissionPackage package, CancellationToken cancellationToken)
    {
        _queue.Enqueue(package);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BallotSubmissionPackage>> DrainAsync(CancellationToken cancellationToken)
    {
        var drained = new List<BallotSubmissionPackage>();

        while (_queue.TryDequeue(out var package))
        {
            drained.Add(package);
        }

        return Task.FromResult<IReadOnlyList<BallotSubmissionPackage>>(drained);
    }
}
