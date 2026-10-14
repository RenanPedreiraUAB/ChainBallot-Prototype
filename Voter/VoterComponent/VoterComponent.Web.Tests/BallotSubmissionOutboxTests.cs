using System.Security.Cryptography;
using System.Text;
using ChainBallot.PublicContracts;
using VoterComponent.Web.Services;

namespace VoterComponent.Web.Tests;

public sealed class BallotSubmissionOutboxTests
{
    [Fact]
    public void FileBackedOutbox_PersistsAcrossRecreation_AndPreservesFifo()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChainBallot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var storagePath = Path.Combine(root, "submission-outbox.json");

        try
        {
            var outbox1 = new FileBackedBallotSubmissionOutbox(storagePath);
            outbox1.Store(CreatePackage("election-1", "nonce-a", "seed-a"), "fail-a");
            outbox1.Store(CreatePackage("election-1", "nonce-b", "seed-b"), "fail-b");

            var outbox2 = new FileBackedBallotSubmissionOutbox(storagePath);
            Assert.Equal(2, outbox2.Snapshot().Count);

            Assert.True(outbox2.TryTakeNext(out var first));
            Assert.Equal("nonce-a", first.Package.SubmissionNonce);

            var outbox3 = new FileBackedBallotSubmissionOutbox(storagePath);
            var snapshot = outbox3.Snapshot();
            Assert.Single(snapshot);
            Assert.Equal("nonce-b", snapshot.Single().Package.SubmissionNonce);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void InMemoryOutbox_RequeueIncrementsRetryCount_AndDeduplicatesByNullifier()
    {
        var outbox = new InMemoryBallotSubmissionOutbox();
        var package = CreatePackage("election-1", "nonce-a", "seed-a");

        outbox.Store(package, "initial failure");
        Assert.True(outbox.TryTakeNext(out var pending));

        outbox.Requeue(pending, "retry failed");
        outbox.Store(package, "duplicate failure event");

        var snapshot = outbox.Snapshot();
        Assert.Single(snapshot);

        var item = snapshot.Single();
        Assert.Equal("election-1", item.Package.ElectionId);
        Assert.Equal(package.BallotNullifier, item.Package.BallotNullifier);
        Assert.True(item.RetryCount >= 1);
    }

    [Fact]
    public void Stage_PersistsEntryAsStaged_BeforeAuthorizationIsMarked()
    {
        var outbox = new InMemoryBallotSubmissionOutbox();
        var package = CreatePackage("election-1", "nonce-a", "seed-a");
        var grantId = Guid.NewGuid();

        outbox.Stage(package, grantId);

        var entry = Assert.Single(outbox.Snapshot());
        Assert.Equal(SubmissionStatus.Staged, entry.Status);
        Assert.Equal(grantId, entry.GrantId);
    }

    [Fact]
    public void MarkAuthorized_TransitionsStagedEntry_InPlace_WithoutLosingItFromTheStore()
    {
        var outbox = new InMemoryBallotSubmissionOutbox();
        var package = CreatePackage("election-1", "nonce-a", "seed-a");
        outbox.Stage(package, Guid.NewGuid());

        var marked = outbox.MarkAuthorized(package.ElectionId, package.BallotNullifier);

        Assert.True(marked);
        var entry = Assert.Single(outbox.Snapshot());
        Assert.Equal(SubmissionStatus.AuthorizedPendingPublish, entry.Status);
    }

    [Fact]
    public void MarkAuthorized_ForUnknownEntry_ReturnsFalse()
    {
        var outbox = new InMemoryBallotSubmissionOutbox();

        Assert.False(outbox.MarkAuthorized("election-unknown", "nullifier-unknown"));
    }

    [Fact]
    public void FileBackedOutbox_StagedEntry_PersistsAcrossRecreation()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChainBallot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var storagePath = Path.Combine(root, "submission-outbox.json");

        try
        {
            var grantId = Guid.NewGuid();
            var outbox1 = new FileBackedBallotSubmissionOutbox(storagePath);
            outbox1.Stage(CreatePackage("election-1", "nonce-a", "seed-a"), grantId);

            var outbox2 = new FileBackedBallotSubmissionOutbox(storagePath);
            var entry = Assert.Single(outbox2.Snapshot());
            Assert.Equal(SubmissionStatus.Staged, entry.Status);
            Assert.Equal(grantId, entry.GrantId);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void FileBackedOutbox_CompactRemovesExpiredAndExceededRetry()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChainBallot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var storagePath = Path.Combine(root, "submission-outbox.json");

        try
        {
            var outbox = new FileBackedBallotSubmissionOutbox(storagePath);
            var package1 = CreatePackage("election-1", "nonce-a", "seed-a");
            var package2 = CreatePackage("election-1", "nonce-b", "seed-b");

            outbox.Store(package1, "fail-a");
            outbox.Store(package2, "fail-b");

            Assert.True(outbox.TryTakeNext(out var first));
            outbox.Requeue(first, "retry-failed");
            outbox.Requeue(first with { RetryCount = 9 }, "retry-failed-again");

            // Compact with strict retry threshold to force removal of high-retry entries.
            var removed = outbox.Compact(DateTimeOffset.UtcNow, TimeSpan.FromHours(1), maxRetryCount: 1, maxEntries: 100);

            Assert.True(removed >= 1);
            Assert.All(outbox.Snapshot(), item => Assert.True(item.RetryCount <= 1));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static BallotSubmissionPackage CreatePackage(string electionId, string nonce, string commitmentSeed)
    {
        var commitment = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(commitmentSeed))).ToLowerInvariant();
        var nullifier = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{electionId}|VID-001|{nonce}"))).ToLowerInvariant();
        return new BallotSubmissionPackage(
            ElectionId: electionId,
            Vid: "VID-001",
            BallotCommitment: commitment,
            SubmissionNonce: nonce,
            BallotNullifier: nullifier,
            EncryptedVotePayload: new EncryptedBallotPayload("A256GCM", "nonce", "tag", "cipher"),
            CreatedAtUtc: DateTimeOffset.UtcNow);
    }
}