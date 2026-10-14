using ChainBallot.PublicContracts;
using ElectionProcessing.Api.Application;
using ElectionProcessing.Api.Processing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElectionProcessing.Api.Tests;

public sealed class OutboundOpenPointsTests
{
    [Fact]
    public async Task ProcessingCoordinator_RejectsDuplicateNullifierWithinBatch()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var queue = new InMemoryBallotQueueAdapter();
        var options = Options.Create(new ProcessingOptions
        {
            MaxBallotsPerBatch = 8,
            MaxBatchWaitSeconds = 1,
            PollIntervalMilliseconds = 1000,
            SettlementNetwork = "solana-devnet",
            SettlementProgramId = "program-1",
            SettlementCommitment = "confirmed"
        });

        var coordinator = new ProcessingCoordinator(
            queueIngress: queue,
            queueConsumer: queue,
            batchValidationService: new BatchValidationService(),
            treeStrategy: new BinarySha256MerkleTreeStrategy(),
            zkProofStrategy: new DeterministicStubZkProofStrategy(),
            proofArtifactVerifier: new DeterministicStubProofArtifactVerifier(),
            settlementAnchor: new MockSettlementAnchor(options),
            projectionEvidencePublisher: new NoOpProjectionEvidencePublisher(),
            authoritativeDigestSource: new NullAuthoritativeDigestSource(),
            clock: clock,
            options: options,
            logger: NullLogger<ProcessingCoordinator>.Instance);

        await coordinator.EnqueueAsync(CreatePackage("election-dup", "nonce-dup", "commit-1"), CancellationToken.None);
        await coordinator.EnqueueAsync(CreatePackage("election-dup", "nonce-dup", "commit-2"), CancellationToken.None);

        await coordinator.RunCycleAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(2));
        await coordinator.RunCycleAsync(CancellationToken.None);

        var state = coordinator.GetElectionState("election-dup");
        Assert.Equal(1, state.AcceptedBallotCount);
        Assert.Equal(1, state.RejectedBallotCount);
        Assert.Equal(1, state.AnchoredBallotCount);
    }

    [Fact]
    public async Task ProcessingCoordinator_RejectsPreviouslySeenNullifierAcrossBatches()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var queue = new InMemoryBallotQueueAdapter();
        var options = Options.Create(new ProcessingOptions
        {
            MaxBallotsPerBatch = 1,
            MaxBatchWaitSeconds = 5,
            PollIntervalMilliseconds = 1000,
            SettlementNetwork = "solana-devnet",
            SettlementProgramId = "program-1",
            SettlementCommitment = "confirmed"
        });

        var coordinator = new ProcessingCoordinator(
            queueIngress: queue,
            queueConsumer: queue,
            batchValidationService: new BatchValidationService(),
            treeStrategy: new BinarySha256MerkleTreeStrategy(),
            zkProofStrategy: new DeterministicStubZkProofStrategy(),
            proofArtifactVerifier: new DeterministicStubProofArtifactVerifier(),
            settlementAnchor: new MockSettlementAnchor(options),
            projectionEvidencePublisher: new NoOpProjectionEvidencePublisher(),
            authoritativeDigestSource: new NullAuthoritativeDigestSource(),
            clock: clock,
            options: options,
            logger: NullLogger<ProcessingCoordinator>.Instance);

        await coordinator.EnqueueAsync(CreatePackage("election-dup2", "nonce-x", "commit-a"), CancellationToken.None);
        await coordinator.RunCycleAsync(CancellationToken.None);

        await coordinator.EnqueueAsync(CreatePackage("election-dup2", "nonce-x", "commit-b"), CancellationToken.None);
        await coordinator.RunCycleAsync(CancellationToken.None);

        var state = coordinator.GetElectionState("election-dup2");
        Assert.Equal(1, state.AnchoredBallotCount);
        Assert.Equal(1, state.RejectedBallotCount);
    }

    [Fact]
    public async Task ProcessingCoordinator_RejectsNonCanonicalNullifier()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var queue = new InMemoryBallotQueueAdapter();
        var options = Options.Create(new ProcessingOptions
        {
            MaxBallotsPerBatch = 1,
            MaxBatchWaitSeconds = 5,
            PollIntervalMilliseconds = 1000,
            SettlementNetwork = "solana-devnet",
            SettlementProgramId = "program-1",
            SettlementCommitment = "confirmed"
        });

        var coordinator = new ProcessingCoordinator(
            queueIngress: queue,
            queueConsumer: queue,
            batchValidationService: new BatchValidationService(),
            treeStrategy: new BinarySha256MerkleTreeStrategy(),
            zkProofStrategy: new DeterministicStubZkProofStrategy(),
            proofArtifactVerifier: new DeterministicStubProofArtifactVerifier(),
            settlementAnchor: new MockSettlementAnchor(options),
            projectionEvidencePublisher: new NoOpProjectionEvidencePublisher(),
            authoritativeDigestSource: new NullAuthoritativeDigestSource(),
            clock: clock,
            options: options,
            logger: NullLogger<ProcessingCoordinator>.Instance);

        await coordinator.EnqueueAsync(CreatePackage("election-nc", "nonce-x", "commit-a", overrideNullifier: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"), CancellationToken.None);
        await coordinator.RunCycleAsync(CancellationToken.None);

        var state = coordinator.GetElectionState("election-nc");
        Assert.Equal(0, state.AnchoredBallotCount);
        Assert.Equal(1, state.RejectedBallotCount);
    }

    [Fact]
    public async Task EnqueueAsync_RejectsMalformedVid()
    {
        var coordinator = CreateValidationCoordinator();
        var package = CreatePackage("election-val", "nonce-vid", "commit-1") with { Vid = "not-a-valid-vid" };

        var result = await coordinator.EnqueueAsync(package, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains("VID", result.RejectionReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, coordinator.GetElectionState("election-val").PendingBallotCount);
    }

    [Fact]
    public async Task EnqueueAsync_RejectsMalformedCommitment()
    {
        var coordinator = CreateValidationCoordinator();
        var package = CreatePackage("election-val", "nonce-commit", "commit-1") with { BallotCommitment = "not-hex!" };

        var result = await coordinator.EnqueueAsync(package, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains("commitment", result.RejectionReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, coordinator.GetElectionState("election-val").PendingBallotCount);
    }

    [Fact]
    public async Task EnqueueAsync_RejectsMalformedEncryptedEnvelope()
    {
        var coordinator = CreateValidationCoordinator();
        var package = CreatePackage("election-val", "nonce-envelope", "commit-1")
            with { EncryptedVotePayload = new EncryptedBallotPayload("A256GCM", "not-base64!!", "dGFn", "Y2lwaGVy") };

        var result = await coordinator.EnqueueAsync(package, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains("envelope", result.RejectionReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, coordinator.GetElectionState("election-val").PendingBallotCount);
    }

    [Fact]
    public async Task EnqueueAsync_AcceptsWellFormedPackage()
    {
        var coordinator = CreateValidationCoordinator();
        var package = CreatePackage("election-val", "nonce-ok", "commit-1");

        var result = await coordinator.EnqueueAsync(package, CancellationToken.None);
        await coordinator.RunCycleAsync(CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.RejectionReason);
        Assert.Equal(1, coordinator.GetElectionState("election-val").PendingBallotCount);
    }

    private static ProcessingCoordinator CreateValidationCoordinator()
    {
        var options = Options.Create(new ProcessingOptions
        {
            MaxBallotsPerBatch = 8,
            MaxBatchWaitSeconds = 5,
            PollIntervalMilliseconds = 1000,
            SettlementNetwork = "solana-devnet",
            SettlementProgramId = "program-1",
            SettlementCommitment = "confirmed"
        });

        var queue = new InMemoryBallotQueueAdapter();
        return new ProcessingCoordinator(
            queueIngress: queue,
            queueConsumer: queue,
            batchValidationService: new BatchValidationService(),
            treeStrategy: new BinarySha256MerkleTreeStrategy(),
            zkProofStrategy: new DeterministicStubZkProofStrategy(),
            proofArtifactVerifier: new DeterministicStubProofArtifactVerifier(),
            settlementAnchor: new MockSettlementAnchor(options),
            projectionEvidencePublisher: new NoOpProjectionEvidencePublisher(),
            authoritativeDigestSource: new NullAuthoritativeDigestSource(),
            clock: new TestClock(DateTimeOffset.Parse("2026-01-01T00:00:00Z")),
            options: options,
            logger: NullLogger<ProcessingCoordinator>.Instance);
    }

    [Fact]
    public async Task ProverReadinessService_LocalProcessReportsNotReadyWhenBinaryMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Processing:QueueMode"] = QueueModes.InMemory,
                ["Processing:MaxBallotsPerBatch"] = "32",
                ["Processing:MaxBatchWaitSeconds"] = "20",
                ["Processing:PollIntervalMilliseconds"] = "1000",
                ["Processing:ProverMode"] = ProverModes.LocalProcess,
                ["Processing:LocalProcessProver:ExecutablePath"] = "C:/does/not/exist/prover.exe",
                ["Processing:LocalProcessProver:TimeoutSeconds"] = "60"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddElectionProcessingCore(configuration);

        using var provider = services.BuildServiceProvider();
        var readiness = provider.GetRequiredService<IProverReadinessService>();
        var snapshot = await readiness.GetReadinessAsync(CancellationToken.None);

        Assert.False(snapshot.IsReady);
        Assert.Contains("not found", snapshot.Reason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private const string TestVid = "vid_0123456789abcdef0123456789abcdef0123";

    private static BallotSubmissionPackage CreatePackage(string electionId, string nonce, string commitmentSeed, string? overrideNullifier = null)
    {
        var commitment = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(commitmentSeed))).ToLowerInvariant();
        var nullifier = overrideNullifier ?? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{electionId}|{TestVid}|{nonce}"))).ToLowerInvariant();
        return new BallotSubmissionPackage(
            ElectionId: electionId,
            Vid: TestVid,
            BallotCommitment: commitment,
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

    private sealed class NullAuthoritativeDigestSource : IAuthoritativeDigestSource
    {
        public Task<string?> GetMetadataDigestAsync(string electionId, CancellationToken cancellationToken) => Task.FromResult<string?>(null);

        public Task<string?> GetAuditDigestAsync(string electionId, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }
}
