using System.Collections.Concurrent;
using ChainBallot.PublicContracts;
using VoterComponent.Web.Contracts;

namespace VoterComponent.Web.Services;

/// <summary>
/// In-memory <see cref="IQueuePublisher"/> implementation. Retained strictly for unit/component tests
/// and local fallback — the canonical runtime transport is <see cref="KafkaBallotQueuePublisher"/>
/// (Architecture.md § 3.5 / Prototype.md § 4.5).
/// </summary>
public sealed class InMemoryBallotQueuePublisher : IQueuePublisher
{
    private readonly ConcurrentQueue<BallotSubmissionPackage> _queue = new();

    public Task<QueueReceipt> PublishAsync(BallotSubmissionPackage package, CancellationToken cancellationToken)
    {
        _queue.Enqueue(package);

        return Task.FromResult(new QueueReceipt(
            QueueItemId: Guid.NewGuid(),
            QueuedAtUtc: DateTimeOffset.UtcNow,
            QueueMode: "InMemory"));
    }

    public IReadOnlyCollection<BallotSubmissionPackage> Snapshot()
    {
        return _queue.ToArray();
    }
}