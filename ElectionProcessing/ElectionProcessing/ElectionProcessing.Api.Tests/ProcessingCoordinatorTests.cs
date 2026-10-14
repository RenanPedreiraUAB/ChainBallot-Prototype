using ChainBallot.PublicContracts;
using ElectionProcessing.Api.Application;
using ElectionProcessing.Api.Processing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElectionProcessing.Api.Tests;

public sealed class ProcessingCoordinatorTests
{
    [Fact]
    public async Task RunCycle_ClosesBatch_WhenMaxCountReached()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var queue = new InMemoryBallotQueueAdapter();
        var coordinator = CreateCoordinator(queue, clock, maxBallotsPerBatch: 2, maxBatchWaitSeconds: 20);

        await coordinator.EnqueueAsync(CreatePackage("election-a", "nonce-1"), CancellationToken.None);
        await coordinator.EnqueueAsync(CreatePackage("election-a", "nonce-2"), CancellationToken.None);

        await coordinator.RunCycleAsync(CancellationToken.None);

        var state = coordinator.GetElectionState("election-a");
        Assert.NotNull(state.LastAnchoredBatch);
        Assert.Equal(2, state.LastAnchoredBatch!.BallotCount);
        Assert.False(state.IsBlocked);
    }

    [Fact]
    public async Task RunCycle_ClosesBatch_WhenMaxWaitElapsed()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var queue = new InMemoryBallotQueueAdapter();
        var coordinator = CreateCoordinator(queue, clock, maxBallotsPerBatch: 10, maxBatchWaitSeconds: 5);

        await coordinator.EnqueueAsync(CreatePackage("election-b", "nonce-1"), CancellationToken.None);

        await coordinator.RunCycleAsync(CancellationToken.None);

        var beforeWait = coordinator.GetElectionState("election-b");
        Assert.Null(beforeWait.LastAnchoredBatch);
        Assert.Equal(1, beforeWait.PendingBallotCount);

        clock.Advance(TimeSpan.FromSeconds(6));
        await coordinator.RunCycleAsync(CancellationToken.None);

        var afterWait = coordinator.GetElectionState("election-b");
        Assert.NotNull(afterWait.LastAnchoredBatch);
        Assert.Equal(1, afterWait.LastAnchoredBatch!.BallotCount);
        Assert.Equal(0, afterWait.PendingBallotCount);
    }

    [Fact]
    public async Task RunCycle_BlocksOnlyFailedElectionQueue()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var queue = new InMemoryBallotQueueAdapter();
        var anchor = new FailElectionOnceAnchor("election-blocked");

        var coordinator = CreateCoordinator(
            queue,
            clock,
            maxBallotsPerBatch: 1,
            maxBatchWaitSeconds: 5,
            settlementAnchor: anchor);

        await coordinator.EnqueueAsync(CreatePackage("election-blocked", "nonce-1"), CancellationToken.None);
        await coordinator.EnqueueAsync(CreatePackage("election-ok", "nonce-2"), CancellationToken.None);

        await coordinator.RunCycleAsync(CancellationToken.None);

        var blockedState = coordinator.GetElectionState("election-blocked");
        var okState = coordinator.GetElectionState("election-ok");

        Assert.True(blockedState.IsBlocked);
        Assert.NotNull(blockedState.FailedBatch);
        Assert.NotNull(okState.LastAnchoredBatch);
        Assert.False(okState.IsBlocked);

        await coordinator.EnqueueAsync(CreatePackage("election-blocked", "nonce-3"), CancellationToken.None);
        await coordinator.RunCycleAsync(CancellationToken.None);

        var stillBlocked = coordinator.GetElectionState("election-blocked");
        Assert.True(stillBlocked.IsBlocked);
        Assert.Equal(1, stillBlocked.PendingBallotCount);

        var retried = await coordinator.RetryFailedBatchAsync("election-blocked", CancellationToken.None);
        Assert.Equal(BatchRetryOutcome.Retried, retried);

        await coordinator.RunCycleAsync(CancellationToken.None);

        var recovered = coordinator.GetElectionState("election-blocked");
        Assert.False(recovered.IsBlocked);
        Assert.NotNull(recovered.LastAnchoredBatch);
    }

    [Fact]
    public async Task RunCycle_WhenProofVerificationFails_BlocksElectionBeforeAnchor()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var queue = new InMemoryBallotQueueAdapter();
        var anchor = new CountingAnchor();

        var coordinator = CreateCoordinator(
            queue,
            clock,
            maxBallotsPerBatch: 1,
            maxBatchWaitSeconds: 5,
            settlementAnchor: anchor,
            proofArtifactVerifier: new RejectingProofVerifier());

        await coordinator.EnqueueAsync(CreatePackage("election-verify", "nonce-1"), CancellationToken.None);
        await coordinator.RunCycleAsync(CancellationToken.None);

        var state = coordinator.GetElectionState("election-verify");
        Assert.True(state.IsBlocked);
        Assert.Contains("verification", state.BlockReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, anchor.AnchorCalls);
    }

    [Fact]
    public async Task RetryFailedBatch_WhenProofVerificationFails_NeverInvokesSettlementAdapter_EvenAfterMultipleRetries()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var queue = new InMemoryBallotQueueAdapter();
        var anchor = new CountingAnchor();

        var coordinator = CreateCoordinator(
            queue,
            clock,
            maxBallotsPerBatch: 1,
            maxBatchWaitSeconds: 5,
            settlementAnchor: anchor,
            proofArtifactVerifier: new RejectingProofVerifier());

        await coordinator.EnqueueAsync(CreatePackage("election-retry-blocked", "nonce-1"), CancellationToken.None);
        await coordinator.RunCycleAsync(CancellationToken.None);

        var blocked = coordinator.GetElectionState("election-retry-blocked");
        Assert.True(blocked.IsBlocked);
        Assert.NotNull(blocked.FailedBatch);
        Assert.Equal(0, anchor.AnchorCalls);

        var firstRetry = await coordinator.RetryFailedBatchAsync("election-retry-blocked", CancellationToken.None);
        var secondRetry = await coordinator.RetryFailedBatchAsync("election-retry-blocked", CancellationToken.None);

        Assert.Equal(BatchRetryOutcome.ProofVerificationFailed, firstRetry);
        Assert.Equal(BatchRetryOutcome.ProofVerificationFailed, secondRetry);
        Assert.Equal(0, anchor.AnchorCalls);

        var stillBlocked = coordinator.GetElectionState("election-retry-blocked");
        Assert.True(stillBlocked.IsBlocked);
        Assert.NotNull(stillBlocked.FailedBatch);
    }

    [Fact]
    public async Task RetryFailedBatch_ReVerifiesProof_AndSettlesOnlyWhenValid()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var queue = new InMemoryBallotQueueAdapter();
        var anchor = new FailElectionOnceAnchor("election-retry-ok");
        var verifier = new CountingProofVerifier(new DeterministicStubProofArtifactVerifier());

        var coordinator = CreateCoordinator(
            queue,
            clock,
            maxBallotsPerBatch: 1,
            maxBatchWaitSeconds: 5,
            settlementAnchor: anchor,
            proofArtifactVerifier: verifier);

        await coordinator.EnqueueAsync(CreatePackage("election-retry-ok", "nonce-1"), CancellationToken.None);
        await coordinator.RunCycleAsync(CancellationToken.None);

        Assert.True(coordinator.GetElectionState("election-retry-ok").IsBlocked);
        var callsAfterInitial = verifier.VerifyCalls;
        Assert.True(callsAfterInitial >= 1);

        var outcome = await coordinator.RetryFailedBatchAsync("election-retry-ok", CancellationToken.None);

        Assert.Equal(BatchRetryOutcome.Retried, outcome);
        Assert.True(verifier.VerifyCalls > callsAfterInitial, "Retry must re-invoke proof verification.");

        var recovered = coordinator.GetElectionState("election-retry-ok");
        Assert.False(recovered.IsBlocked);
        Assert.NotNull(recovered.LastAnchoredBatch);
    }

    [Fact]
    public async Task RetryFailedBatch_ConcurrentRetries_OnlyOneCanReachSettlement()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var queue = new InMemoryBallotQueueAdapter();
        var anchor = new FailElectionOnceAnchor("election-concurrent-retry");

        var coordinator = CreateCoordinator(
            queue,
            clock,
            maxBallotsPerBatch: 1,
            maxBatchWaitSeconds: 5,
            settlementAnchor: anchor);

        await coordinator.EnqueueAsync(CreatePackage("election-concurrent-retry", "nonce-1"), CancellationToken.None);
        await coordinator.RunCycleAsync(CancellationToken.None);

        Assert.True(coordinator.GetElectionState("election-concurrent-retry").IsBlocked);
        Assert.Equal(1, anchor.AnchorCalls);

        var outcomes = await Task.WhenAll(
            coordinator.RetryFailedBatchAsync("election-concurrent-retry", CancellationToken.None),
            coordinator.RetryFailedBatchAsync("election-concurrent-retry", CancellationToken.None));

        Assert.Single(outcomes, o => o == BatchRetryOutcome.Retried);
        Assert.Single(outcomes, o => o == BatchRetryOutcome.NoFailedBatch);
        Assert.Equal(2, anchor.AnchorCalls);

        var recovered = coordinator.GetElectionState("election-concurrent-retry");
        Assert.False(recovered.IsBlocked);
        Assert.NotNull(recovered.LastAnchoredBatch);
    }

    [Fact]
    public async Task RetryFailedBatch_AfterSuccess_SecondCallIsNoOp_AndDoesNotSettleAgain()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var queue = new InMemoryBallotQueueAdapter();
        var anchor = new FailElectionOnceAnchor("election-retry-twice");

        var coordinator = CreateCoordinator(
            queue,
            clock,
            maxBallotsPerBatch: 1,
            maxBatchWaitSeconds: 5,
            settlementAnchor: anchor);

        await coordinator.EnqueueAsync(CreatePackage("election-retry-twice", "nonce-1"), CancellationToken.None);
        await coordinator.RunCycleAsync(CancellationToken.None);

        var firstRetry = await coordinator.RetryFailedBatchAsync("election-retry-twice", CancellationToken.None);
        Assert.Equal(BatchRetryOutcome.Retried, firstRetry);
        Assert.Equal(2, anchor.AnchorCalls);

        var secondRetry = await coordinator.RetryFailedBatchAsync("election-retry-twice", CancellationToken.None);
        Assert.Equal(BatchRetryOutcome.NoFailedBatch, secondRetry);
        Assert.Equal(2, anchor.AnchorCalls);
    }

    [Fact]
    public async Task RunCycle_PublishesProjectionEvidence_WhenBatchAnchors()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var queue = new InMemoryBallotQueueAdapter();
        var projectionPublisher = new CapturingProjectionEvidencePublisher();
        var coordinator = CreateCoordinator(
            queue,
            clock,
            maxBallotsPerBatch: 1,
            maxBatchWaitSeconds: 5,
            projectionEvidencePublisher: projectionPublisher);

        await coordinator.EnqueueAsync(CreatePackage("election-proj", "nonce-1"), CancellationToken.None);
        await coordinator.RunCycleAsync(CancellationToken.None);

        var published = Assert.Single(projectionPublisher.Published);
        Assert.Equal("election-proj", published.ElectionId);
        Assert.Single(published.VidEntries);
        Assert.Equal(1, published.CandidateTallies["anchoredBallots"]);
    }

    [Fact]
    public async Task RunCycle_AttachesAuthoritativeDigests_ToPublishedEvidence()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var queue = new InMemoryBallotQueueAdapter();
        var projectionPublisher = new CapturingProjectionEvidencePublisher();
        var coordinator = CreateCoordinator(
            queue,
            clock,
            maxBallotsPerBatch: 1,
            maxBatchWaitSeconds: 5,
            projectionEvidencePublisher: projectionPublisher,
            authoritativeDigestSource: new FakeAuthoritativeDigestSource("metadata-digest-v1", "audit-digest-v1"));

        await coordinator.EnqueueAsync(CreatePackage("election-digest", "nonce-1"), CancellationToken.None);
        await coordinator.RunCycleAsync(CancellationToken.None);

        var published = Assert.Single(projectionPublisher.Published);
        Assert.Equal("metadata-digest-v1", published.MetadataDigest);
        Assert.Equal("audit-digest-v1", published.AuditDigest);
    }

    private static ProcessingCoordinator CreateCoordinator(
        InMemoryBallotQueueAdapter queue,
        ISystemClock clock,
        int maxBallotsPerBatch,
        int maxBatchWaitSeconds,
        ISettlementAnchor? settlementAnchor = null,
        IProofArtifactVerifier? proofArtifactVerifier = null,
        IProjectionEvidencePublisher? projectionEvidencePublisher = null,
        IAuthoritativeDigestSource? authoritativeDigestSource = null)
    {
        var options = Options.Create(new ProcessingOptions
        {
            MaxBallotsPerBatch = maxBallotsPerBatch,
            MaxBatchWaitSeconds = maxBatchWaitSeconds,
            PollIntervalMilliseconds = 1000,
            SettlementNetwork = "solana-devnet",
            SettlementProgramId = "program-1",
            SettlementCommitment = "confirmed"
        });

        return new ProcessingCoordinator(
            queueIngress: queue,
            queueConsumer: queue,
            batchValidationService: new BatchValidationService(),
            treeStrategy: new BinarySha256MerkleTreeStrategy(),
            zkProofStrategy: new DeterministicStubZkProofStrategy(),
            proofArtifactVerifier: proofArtifactVerifier ?? new DeterministicStubProofArtifactVerifier(),
            settlementAnchor: settlementAnchor ?? new MockSettlementAnchor(options),
            projectionEvidencePublisher: projectionEvidencePublisher ?? new NoOpProjectionEvidencePublisher(),
            authoritativeDigestSource: authoritativeDigestSource ?? new FakeAuthoritativeDigestSource(null, null),
            clock: clock,
            options: options,
            logger: NullLogger<ProcessingCoordinator>.Instance);
    }

    private const string TestVid = "vid_0123456789abcdef0123456789abcdef0123";

    private static BallotSubmissionPackage CreatePackage(string electionId, string nonce)
    {
        var nullifier = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{electionId}|{TestVid}|{nonce}"))).ToLowerInvariant();
        return new BallotSubmissionPackage(
            ElectionId: electionId,
            Vid: TestVid,
            BallotCommitment: Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant(),
            SubmissionNonce: nonce,
            BallotNullifier: nullifier,
            EncryptedVotePayload: new EncryptedBallotPayload("A256GCM", "bm9uY2U=", "dGFn", "Y2lwaGVy"),
            CreatedAtUtc: DateTimeOffset.UtcNow);
    }

    private sealed class TestClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;

        public void Advance(TimeSpan by)
        {
            UtcNow = UtcNow.Add(by);
        }
    }

    private sealed class FailElectionOnceAnchor(string electionId) : ISettlementAnchor
    {
        private int _failed;

        public int AnchorCalls { get; private set; }

        public Task<SettlementReference> AnchorAsync(BatchAnchorRequest request, CancellationToken cancellationToken)
        {
            AnchorCalls++;
            if (request.ElectionId == electionId && Interlocked.CompareExchange(ref _failed, 1, 0) == 0)
            {
                throw new InvalidOperationException("Intentional anchor failure for test.");
            }

            return Task.FromResult(new SettlementReference(
                Network: "solana-devnet",
                ProgramId: "program-1",
                ElectionId: request.ElectionId,
                TransactionSignature: $"sig-{request.BatchId}",
                Slot: 123,
                BlockTimeUtc: DateTimeOffset.UtcNow,
                Commitment: "confirmed"));
        }
    }

    private sealed class CountingAnchor : ISettlementAnchor
    {
        public int AnchorCalls { get; private set; }

        public Task<SettlementReference> AnchorAsync(BatchAnchorRequest request, CancellationToken cancellationToken)
        {
            AnchorCalls++;
            return Task.FromResult(new SettlementReference(
                Network: "solana-devnet",
                ProgramId: "program-1",
                ElectionId: request.ElectionId,
                TransactionSignature: $"sig-{request.BatchId}",
                Slot: 999,
                BlockTimeUtc: DateTimeOffset.UtcNow,
                Commitment: "confirmed"));
        }
    }

    private sealed class RejectingProofVerifier : IProofArtifactVerifier
    {
        public string Name => "RejectingProofVerifier";

        public Task<ProofVerificationResult> VerifyAsync(BatchComputationInput input, ProofArtifact proofArtifact, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ProofVerificationResult(false, "Forced verification failure for test."));
        }
    }

    private sealed class CountingProofVerifier(IProofArtifactVerifier inner) : IProofArtifactVerifier
    {
        public int VerifyCalls { get; private set; }

        public string Name => inner.Name;

        public Task<ProofVerificationResult> VerifyAsync(BatchComputationInput input, ProofArtifact proofArtifact, CancellationToken cancellationToken)
        {
            VerifyCalls++;
            return inner.VerifyAsync(input, proofArtifact, cancellationToken);
        }
    }

    private sealed class CapturingProjectionEvidencePublisher : IProjectionEvidencePublisher
    {
        public List<ProjectionEvidenceBatch> Published { get; } = new();

        public Task PublishBatchAnchoredAsync(ProjectionEvidenceBatch batch, CancellationToken cancellationToken)
        {
            Published.Add(batch);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuthoritativeDigestSource(string? metadataDigest, string? auditDigest) : IAuthoritativeDigestSource
    {
        public Task<string?> GetMetadataDigestAsync(string electionId, CancellationToken cancellationToken) => Task.FromResult(metadataDigest);

        public Task<string?> GetAuditDigestAsync(string electionId, CancellationToken cancellationToken) => Task.FromResult(auditDigest);
    }
}
