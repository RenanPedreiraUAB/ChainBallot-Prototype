using ChainBallot.PublicContracts;

namespace VoterComponent.Web.Services;

/// <summary>
/// Durable submission-retry boundary for the Voter Component (Architecture.md § 3.4). This is part of
/// the NORMAL submission path, not merely a failure fallback: a package is durably <see cref="Stage"/>d
/// here before its one-time authorization grant is consumed, so a crash at any point after staging still
/// leaves a recoverable record of the submission (see <see cref="FileBackedBallotSubmissionOutbox"/>).
/// SECURITY INVARIANT: no voting right may be consumed before a durable representation of the
/// corresponding ballot submission exists in this outbox.
/// </summary>
public interface IBallotSubmissionOutbox
{
    /// <summary>Durably stages a package before its authorization grant is consumed (normal submission path).</summary>
    void Stage(BallotSubmissionPackage package, Guid grantId);

    /// <summary>Stores a ballot submission that failed to publish, for later retry.</summary>
    void Store(BallotSubmissionPackage package, string reason);

    /// <summary>Transitions a staged (pre-authorization) entry to authorized-pending-publish in place, once its grant has been consumed.</summary>
    bool MarkAuthorized(string electionId, string ballotNullifier);

    /// <summary>Re-queues a pending submission after another failed retry attempt.</summary>
    void Requeue(PendingBallotSubmission pendingSubmission, string reason);

    /// <summary>Attempts to take the next pending submission eligible for retry.</summary>
    bool TryTakeNext(out PendingBallotSubmission pendingSubmission);

    /// <summary>Attempts to remove a specific pending submission, identified by election id and ballot nullifier.</summary>
    bool TryRemove(string electionId, string ballotNullifier, out PendingBallotSubmission removedSubmission);

    /// <summary>Removes expired (TTL), over-retried, or excess (beyond capacity) entries. Returns the number removed.</summary>
    int Compact(DateTimeOffset nowUtc, TimeSpan entryTtl, int maxRetryCount, int maxEntries);

    /// <summary>Returns a read-only snapshot of all currently pending submissions.</summary>
    IReadOnlyCollection<PendingBallotSubmission> Snapshot();

    /// <summary>Computes summary statistics (counts, ages) over the current pending submissions.</summary>
    OutboxStatistics GetStatistics(DateTimeOffset nowUtc, TimeSpan entryTtl, int maxRetryCount);
}

/// <summary>Lifecycle of a durably staged ballot submission (Architecture.md § 3.4).</summary>
public enum SubmissionStatus
{
    /// <summary>Durably persisted; its authorization grant has not yet been consumed.</summary>
    Staged = 0,

    /// <summary>Its authorization grant has been consumed; awaiting a successful broker publish.</summary>
    AuthorizedPendingPublish = 1
}

public sealed record PendingBallotSubmission(
    BallotSubmissionPackage Package,
    string Reason,
    DateTimeOffset FailedAtUtc,
    int RetryCount,
    DateTimeOffset? LastAttemptAtUtc = null,
    string? LastError = null,
    SubmissionStatus Status = SubmissionStatus.AuthorizedPendingPublish,
    Guid? GrantId = null);

public sealed record OutboxStatistics(
    int Total,
    int Ready,
    int Expired,
    int ExceededRetryCount);