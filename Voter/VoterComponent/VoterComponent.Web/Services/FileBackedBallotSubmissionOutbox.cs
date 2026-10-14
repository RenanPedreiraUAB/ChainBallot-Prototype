using System.Text.Json;
using ChainBallot.PublicContracts;

namespace VoterComponent.Web.Services;

/// <summary>
/// Durable, file-backed realization of the Voter Component's submission retry outbox
/// (Architecture.md § 3.4 / Prototype.md § 4.4). Ballots that fail to publish to the message broker
/// are retained here across process restarts, subject to TTL/max-retry/capacity policy, so a transient
/// broker outage cannot silently drop an already-authorized vote.
/// </summary>
public sealed class FileBackedBallotSubmissionOutbox : IBallotSubmissionOutbox
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly object _sync = new();
    private readonly string _storagePath;
    private readonly List<PendingBallotSubmission> _pending;

    public FileBackedBallotSubmissionOutbox(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException("Outbox storage path is required.", nameof(storagePath));
        }

        _storagePath = storagePath;

        var parent = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        _pending = Load();
    }

    public void Stage(BallotSubmissionPackage package, Guid grantId)
    {
        lock (_sync)
        {
            UpsertUnsafe(new PendingBallotSubmission(
                Package: package,
                Reason: "staged pending authorization",
                FailedAtUtc: DateTimeOffset.UtcNow,
                RetryCount: 0,
                Status: SubmissionStatus.Staged,
                GrantId: grantId));
            PersistUnsafe();
        }
    }

    public void Store(BallotSubmissionPackage package, string reason)
    {
        lock (_sync)
        {
            UpsertUnsafe(new PendingBallotSubmission(
                Package: package,
                Reason: reason,
                FailedAtUtc: DateTimeOffset.UtcNow,
                RetryCount: 0));
            PersistUnsafe();
        }
    }

    public bool MarkAuthorized(string electionId, string ballotNullifier)
    {
        lock (_sync)
        {
            var index = _pending.FindIndex(item =>
                string.Equals(item.Package.ElectionId, electionId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Package.BallotNullifier, ballotNullifier, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                return false;
            }

            _pending[index] = _pending[index] with { Status = SubmissionStatus.AuthorizedPendingPublish };
            PersistUnsafe();
            return true;
        }
    }

    public void Requeue(PendingBallotSubmission pendingSubmission, string reason)
    {
        lock (_sync)
        {
            UpsertUnsafe(pendingSubmission with
            {
                FailedAtUtc = DateTimeOffset.UtcNow,
                RetryCount = pendingSubmission.RetryCount + 1,
                LastAttemptAtUtc = DateTimeOffset.UtcNow,
                LastError = reason
            });

            PersistUnsafe();
        }
    }

    public bool TryTakeNext(out PendingBallotSubmission pendingSubmission)
    {
        lock (_sync)
        {
            if (_pending.Count == 0)
            {
                pendingSubmission = default!;
                return false;
            }

            pendingSubmission = _pending[0];
            _pending.RemoveAt(0);
            PersistUnsafe();
            return true;
        }
    }

    public IReadOnlyCollection<PendingBallotSubmission> Snapshot()
    {
        lock (_sync)
        {
            return _pending.ToArray();
        }
    }

    public bool TryRemove(string electionId, string ballotNullifier, out PendingBallotSubmission removedSubmission)
    {
        lock (_sync)
        {
            var index = _pending.FindIndex(item =>
                string.Equals(item.Package.ElectionId, electionId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Package.BallotNullifier, ballotNullifier, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                removedSubmission = default!;
                return false;
            }

            removedSubmission = _pending[index];
            _pending.RemoveAt(index);
            PersistUnsafe();
            return true;
        }
    }

    public int Compact(DateTimeOffset nowUtc, TimeSpan entryTtl, int maxRetryCount, int maxEntries)
    {
        lock (_sync)
        {
            var beforeCount = _pending.Count;

            _pending.RemoveAll(item => nowUtc - item.FailedAtUtc > entryTtl);
            _pending.RemoveAll(item => item.RetryCount > maxRetryCount);
            _pending.Sort((left, right) => left.FailedAtUtc.CompareTo(right.FailedAtUtc));

            if (maxEntries > 0 && _pending.Count > maxEntries)
            {
                var removeCount = _pending.Count - maxEntries;
                _pending.RemoveRange(0, removeCount);
            }

            var removed = beforeCount - _pending.Count;
            if (removed > 0)
            {
                PersistUnsafe();
            }

            return removed;
        }
    }

    public OutboxStatistics GetStatistics(DateTimeOffset nowUtc, TimeSpan entryTtl, int maxRetryCount)
    {
        lock (_sync)
        {
            var total = _pending.Count;
            var expired = _pending.Count(item => nowUtc - item.FailedAtUtc > entryTtl);
            var exceededRetry = _pending.Count(item => item.RetryCount > maxRetryCount);
            var ready = total - expired - exceededRetry;
            if (ready < 0)
            {
                ready = 0;
            }

            return new OutboxStatistics(total, ready, expired, exceededRetry);
        }
    }

    private List<PendingBallotSubmission> Load()
    {
        if (!File.Exists(_storagePath))
        {
            return [];
        }

        var json = File.ReadAllText(_storagePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PendingBallotSubmission>>(json, JsonOptions) ?? [];
        }
        catch
        {
            // If on-disk outbox is malformed, preserve the file and start fresh.
            var backupPath = _storagePath + ".corrupt-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            File.Move(_storagePath, backupPath, overwrite: true);
            return [];
        }
    }

    private void PersistUnsafe()
    {
        var tempPath = _storagePath + ".tmp";
        var json = JsonSerializer.Serialize(_pending, JsonOptions);
        File.WriteAllText(tempPath, json);

        if (File.Exists(_storagePath))
        {
            File.Replace(tempPath, _storagePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, _storagePath);
        }
    }

    private void UpsertUnsafe(PendingBallotSubmission incoming)
    {
        var existingIndex = _pending.FindIndex(item =>
            string.Equals(item.Package.ElectionId, incoming.Package.ElectionId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Package.BallotNullifier, incoming.Package.BallotNullifier, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            var existing = _pending[existingIndex];
            _pending[existingIndex] = incoming with
            {
                RetryCount = Math.Max(existing.RetryCount, incoming.RetryCount),
                FailedAtUtc = DateTimeOffset.UtcNow
            };
            return;
        }

        _pending.Add(incoming);
    }
}