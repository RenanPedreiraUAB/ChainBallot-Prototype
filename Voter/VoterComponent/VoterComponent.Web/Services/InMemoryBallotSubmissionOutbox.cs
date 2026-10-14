using System.Collections.Concurrent;
using ChainBallot.PublicContracts;

namespace VoterComponent.Web.Services;

/// <summary>
/// In-memory <see cref="IBallotSubmissionOutbox"/> implementation. Retained strictly for unit/component
/// tests — the canonical runtime outbox is <see cref="FileBackedBallotSubmissionOutbox"/>, which
/// survives process restarts (Architecture.md § 3.4 / Prototype.md § 4.4).
/// </summary>
public sealed class InMemoryBallotSubmissionOutbox : IBallotSubmissionOutbox
{
    private readonly ConcurrentQueue<PendingBallotSubmission> _pending = new();

    public void Stage(BallotSubmissionPackage package, Guid grantId)
    {
        Upsert(new PendingBallotSubmission(
            package,
            "staged pending authorization",
            DateTimeOffset.UtcNow,
            RetryCount: 0,
            Status: SubmissionStatus.Staged,
            GrantId: grantId));
    }

    public void Store(BallotSubmissionPackage package, string reason)
    {
        Upsert(new PendingBallotSubmission(package, reason, DateTimeOffset.UtcNow, RetryCount: 0));
    }

    public bool MarkAuthorized(string electionId, string ballotNullifier)
    {
        var items = _pending.ToList();
        var index = items.FindIndex(i =>
            string.Equals(i.Package.ElectionId, electionId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(i.Package.BallotNullifier, ballotNullifier, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return false;
        }

        items[index] = items[index] with { Status = SubmissionStatus.AuthorizedPendingPublish };
        Rebuild(items);
        return true;
    }

    public void Requeue(PendingBallotSubmission pendingSubmission, string reason)
    {
        var updated = pendingSubmission with
        {
            FailedAtUtc = DateTimeOffset.UtcNow,
            RetryCount = pendingSubmission.RetryCount + 1,
            LastAttemptAtUtc = DateTimeOffset.UtcNow,
            LastError = reason
        };

        Upsert(updated);
    }

    public bool TryTakeNext(out PendingBallotSubmission pendingSubmission)
    {
        return _pending.TryDequeue(out pendingSubmission!);
    }

    public IReadOnlyCollection<PendingBallotSubmission> Snapshot()
    {
        return _pending.ToArray();
    }

    public bool TryRemove(string electionId, string ballotNullifier, out PendingBallotSubmission removedSubmission)
    {
        var items = _pending.ToList();
        var match = items.FirstOrDefault(i =>
            string.Equals(i.Package.ElectionId, electionId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(i.Package.BallotNullifier, ballotNullifier, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            removedSubmission = default!;
            return false;
        }

        items.Remove(match);
        Rebuild(items);
        removedSubmission = match;
        return true;
    }

    public int Compact(DateTimeOffset nowUtc, TimeSpan entryTtl, int maxRetryCount, int maxEntries)
    {
        var items = _pending.ToList();
        var filtered = items
            .Where(i => nowUtc - i.FailedAtUtc <= entryTtl)
            .Where(i => i.RetryCount <= maxRetryCount)
            .OrderBy(i => i.FailedAtUtc)
            .ToList();

        if (maxEntries > 0 && filtered.Count > maxEntries)
        {
            filtered = filtered.Skip(filtered.Count - maxEntries).ToList();
        }

        var removed = items.Count - filtered.Count;
        if (removed > 0)
        {
            Rebuild(filtered);
        }

        return removed;
    }

    public OutboxStatistics GetStatistics(DateTimeOffset nowUtc, TimeSpan entryTtl, int maxRetryCount)
    {
        var items = _pending.ToArray();
        var expired = items.Count(i => nowUtc - i.FailedAtUtc > entryTtl);
        var exceededRetry = items.Count(i => i.RetryCount > maxRetryCount);
        var ready = items.Length - expired - exceededRetry;
        if (ready < 0)
        {
            ready = 0;
        }

        return new OutboxStatistics(items.Length, ready, expired, exceededRetry);
    }

    private void Upsert(PendingBallotSubmission incoming)
    {
        var items = _pending.ToList();
        var existingIndex = items.FindIndex(i =>
            string.Equals(i.Package.ElectionId, incoming.Package.ElectionId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(i.Package.BallotNullifier, incoming.Package.BallotNullifier, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            var existing = items[existingIndex];
            items[existingIndex] = incoming with
            {
                RetryCount = Math.Max(incoming.RetryCount, existing.RetryCount),
                FailedAtUtc = DateTimeOffset.UtcNow
            };
        }
        else
        {
            items.Add(incoming);
        }

        Rebuild(items);
    }

    private void Rebuild(IReadOnlyCollection<PendingBallotSubmission> items)
    {
        while (_pending.TryDequeue(out _))
        {
        }

        foreach (var item in items.OrderBy(i => i.FailedAtUtc))
        {
            _pending.Enqueue(item);
        }
    }
}