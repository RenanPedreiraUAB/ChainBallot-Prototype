using System.Security.Claims;
using ChainBallot.PublicContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VoterComponent.Web.Application;
using VoterComponent.Web.Contracts;
using VoterComponent.Web.Services;

namespace VoterComponent.Web.Tests;

public sealed class VoterWorkflowServiceTests
{
    [Fact]
    public async Task SubmitBallot_WhenQueuePublishFails_StoresPendingSubmissionForRetry()
    {
        var queuePublisher = new ThrowingQueuePublisher();
        var outbox = new InMemoryBallotSubmissionOutbox();
        var service = new VoterWorkflowService(
            new HttpContextAccessor { HttpContext = CreateAuthenticatedContext() },
            new FakeAuthorizationClient(),
            new FakeMetadataClient(),
            new AesGcmBallotEncryptionService(Options.Create(new VoterOptions { BallotEncryptionKey = "test-key" })),
            new Sha256BallotCommitmentService(),
            queuePublisher,
            outbox,
            Options.Create(new VoterOptions
            {
                DefaultElectionId = "election-demo",
                MetadataEndpointTemplate = "https://metadata.example/{electionId}",
                BallotEncryptionKey = "test-key"
            }),
            NullLogger<VoterWorkflowService>.Instance);

        var result = await service.SubmitBallotAsync(new BallotSelectionRequest("election-demo", 1, "Ada Lovelace"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("retained for retry", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(outbox.Snapshot());
        Assert.Empty(queuePublisher.Published);
    }

    [Fact]
    public async Task SubmitBallot_StagesPackageBeforeConsumingGrant()
    {
        var callOrder = new List<string>();
        var outbox = new RecordingOutbox(callOrder);
        var authorizationClient = new RecordingAuthorizationClient(callOrder);
        var service = CreateService(new InMemoryBallotQueuePublisher(), outbox, authorizationClient);

        var result = await service.SubmitBallotAsync(new BallotSelectionRequest("election-demo", 1, "Ada Lovelace"), CancellationToken.None);

        Assert.True(result.Succeeded);
        var stageIndex = callOrder.IndexOf("Stage");
        var consumeIndex = callOrder.IndexOf("ConsumeGrantAsync");
        Assert.True(stageIndex >= 0, "Stage was never called.");
        Assert.True(consumeIndex >= 0, "ConsumeGrantAsync was never called.");
        Assert.True(stageIndex < consumeIndex, "Durable staging must happen before grant consumption.");
    }

    [Fact]
    public async Task SubmitBallot_WhenDurableStagingFails_DoesNotConsumeGrant()
    {
        var outbox = new RecordingOutbox([]) { ThrowOnStage = true };
        var authorizationClient = new RecordingAuthorizationClient([]);
        var service = CreateService(new InMemoryBallotQueuePublisher(), outbox, authorizationClient);

        var result = await service.SubmitBallotAsync(new BallotSelectionRequest("election-demo", 1, "Ada Lovelace"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(0, authorizationClient.ConsumeGrantCalls);
        Assert.Empty(outbox.Snapshot());
    }

    [Fact]
    public async Task SubmitBallot_WhenAuthorizationConsumptionFails_RemovesStagedEntry_AndNeverPublishes()
    {
        var queuePublisher = new InMemoryBallotQueuePublisher();
        var authorizationClient = new RecordingAuthorizationClient([]) { ConsumeOutcome = AuthorizationConsumeOutcome.AlreadyVoted };
        var outbox = new InMemoryBallotSubmissionOutbox();
        var service = CreateService(queuePublisher, outbox, authorizationClient);

        var result = await service.SubmitBallotAsync(new BallotSelectionRequest("election-demo", 1, "Ada Lovelace"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(outbox.Snapshot());
        Assert.Empty(queuePublisher.Snapshot());
    }

    [Fact]
    public async Task SubmitBallot_CalledTwiceAfterSuccess_SecondAttemptRejected_NoDuplicatePackagePublished()
    {
        var queuePublisher = new InMemoryBallotQueuePublisher();
        var authorizationClient = new RecordingAuthorizationClient([]);
        var outbox = new InMemoryBallotSubmissionOutbox();
        var service = CreateService(queuePublisher, outbox, authorizationClient);
        var request = new BallotSelectionRequest("election-demo", 1, "Ada Lovelace");

        var first = await service.SubmitBallotAsync(request, CancellationToken.None);
        var second = await service.SubmitBallotAsync(request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.Contains("AlreadyVoted", second.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(queuePublisher.Snapshot());
        Assert.Empty(outbox.Snapshot());
    }

    [Fact]
    public async Task RetryPendingSubmissions_RecoversStagedEntry_ConsumesGrantThenPublishes()
    {
        var queuePublisher = new InMemoryBallotQueuePublisher();
        var authorizationClient = new RecordingAuthorizationClient([]);
        var outbox = new InMemoryBallotSubmissionOutbox();
        var service = CreateService(queuePublisher, outbox, authorizationClient);

        var package = CreatePackage("election-demo", "nonce-recover");
        outbox.Stage(package, Guid.NewGuid());

        await service.LoadDashboardAsync("election-demo", CancellationToken.None);

        Assert.Equal(1, authorizationClient.ConsumeGrantCalls);
        Assert.Contains(queuePublisher.Snapshot(), p => p.BallotNullifier == package.BallotNullifier);
        Assert.Empty(outbox.Snapshot());
    }

    [Fact]
    public async Task RetryPendingSubmissions_WhenGrantCanNeverBeAuthorized_DropsStagedEntry_WithoutPublishing()
    {
        var queuePublisher = new InMemoryBallotQueuePublisher();
        var authorizationClient = new RecordingAuthorizationClient([]) { ConsumeOutcome = AuthorizationConsumeOutcome.ExpiredGrant };
        var outbox = new InMemoryBallotSubmissionOutbox();
        var service = CreateService(queuePublisher, outbox, authorizationClient);

        var package = CreatePackage("election-demo", "nonce-expired");
        outbox.Stage(package, Guid.NewGuid());

        await service.LoadDashboardAsync("election-demo", CancellationToken.None);

        Assert.Empty(outbox.Snapshot());
        Assert.Empty(queuePublisher.Snapshot());
    }

    [Fact]
    public async Task RetryPendingSubmissions_ForAlreadyAuthorizedEntry_IsIdempotent_WhenPublishKeepsFailing()
    {
        var queuePublisher = new ThrowingQueuePublisher();
        var authorizationClient = new RecordingAuthorizationClient([]);
        var outbox = new InMemoryBallotSubmissionOutbox();
        var service = CreateService(queuePublisher, outbox, authorizationClient);

        var package = CreatePackage("election-demo", "nonce-authorized");
        outbox.Stage(package, Guid.NewGuid());
        outbox.MarkAuthorized(package.ElectionId, package.BallotNullifier);

        await service.LoadDashboardAsync("election-demo", CancellationToken.None);
        await service.LoadDashboardAsync("election-demo", CancellationToken.None);

        Assert.Equal(0, authorizationClient.ConsumeGrantCalls);
        Assert.Single(outbox.Snapshot());
        Assert.Equal(SubmissionStatus.AuthorizedPendingPublish, outbox.Snapshot().Single().Status);
    }

    private static VoterWorkflowService CreateService(
        IQueuePublisher queuePublisher,
        IBallotSubmissionOutbox outbox,
        IElectionAuthorizationClient authorizationClient)
    {
        return new VoterWorkflowService(
            new HttpContextAccessor { HttpContext = CreateAuthenticatedContext() },
            authorizationClient,
            new FakeMetadataClient(),
            new AesGcmBallotEncryptionService(Options.Create(new VoterOptions { BallotEncryptionKey = "test-key" })),
            new Sha256BallotCommitmentService(),
            queuePublisher,
            outbox,
            Options.Create(new VoterOptions
            {
                DefaultElectionId = "election-demo",
                MetadataEndpointTemplate = "https://metadata.example/{electionId}",
                BallotEncryptionKey = "test-key"
            }),
            NullLogger<VoterWorkflowService>.Instance);
    }

    private static BallotSubmissionPackage CreatePackage(string electionId, string submissionNonce)
    {
        const string vid = "VID-ALICE-001";
        var nullifier = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{electionId}|{vid}|{submissionNonce}"))).ToLowerInvariant();
        var commitment = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{electionId}|{vid}|{submissionNonce}|commitment"))).ToLowerInvariant();

        return new BallotSubmissionPackage(
            ElectionId: electionId,
            Vid: vid,
            BallotCommitment: commitment,
            SubmissionNonce: submissionNonce,
            BallotNullifier: nullifier,
            EncryptedVotePayload: new EncryptedBallotPayload("A256GCM", "nonce", "tag", "cipher"),
            CreatedAtUtc: DateTimeOffset.UtcNow);
    }

    private static DefaultHttpContext CreateAuthenticatedContext()
    {
        var context = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "subject-alice"),
            new Claim(SessionClaimNames.AccessToken, "test-access-token")
        };

        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return context;
    }

    private sealed class ThrowingQueuePublisher : IQueuePublisher
    {
        public List<BallotSubmissionPackage> Published { get; } = [];

        public Task<QueueReceipt> PublishAsync(BallotSubmissionPackage package, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Queue unavailable.");
        }

        public IReadOnlyCollection<BallotSubmissionPackage> Snapshot() => Published;
    }

    private sealed class FakeAuthorizationClient : IElectionAuthorizationClient
    {
        private static readonly VoterAuthorizationProfile Profile = new("subject-alice", "VID-ALICE-001", DateTimeOffset.UtcNow);

        public Task<VoterAuthorizationProfile?> GetProfileAsync(CancellationToken cancellationToken)
            => Task.FromResult<VoterAuthorizationProfile?>(Profile);

        public Task<ElectionAuthorizationState?> GetElectionStateAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult<ElectionAuthorizationState?>(new ElectionAuthorizationState(electionId, Profile.Vid, ElectionAuthorizationStatus.EligibleNotVoted, null));

        public Task<AuthorizationGrant?> IssueGrantAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult<AuthorizationGrant?>(new AuthorizationGrant(Guid.Parse("11111111-1111-1111-1111-111111111111"), electionId, Profile.Vid, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(2)));

        public Task<ConsumeAuthorizationResponse?> ConsumeGrantAsync(string electionId, Guid grantId, string submissionNonce, CancellationToken cancellationToken)
            => Task.FromResult<ConsumeAuthorizationResponse?>(new ConsumeAuthorizationResponse(electionId, Profile.Vid, AuthorizationConsumeOutcome.Consumed, DateTimeOffset.UtcNow, "Authorization consumed."));
    }

    private sealed class FakeMetadataClient : IElectionMetadataClient
    {
        public Task<ElectionMetadataDocument?> GetElectionMetadataAsync(string electionId, CancellationToken cancellationToken)
        {
            var document = new ElectionMetadataDocument(
                new ElectionMetadataElection(electionId, "City Election", "Local office ballot", "1.0.0"),
                new ElectionMetadataVoting(1000, 2000),
                new ElectionMetadataBlockchain("devnet", "program-1", "pda-1"),
                new[]
                {
                    new ElectionMetadataCandidate(1, "Ada Lovelace", "Science", "Independent candidate", "ada.png")
                },
                new ElectionMetadataVerification("abc123", 1234567890));

            return Task.FromResult<ElectionMetadataDocument?>(document);
        }
    }

    /// <summary>Records call order/counts and lets the consume outcome and eligibility state be controlled per test.</summary>
    private sealed class RecordingAuthorizationClient(List<string> callOrder) : IElectionAuthorizationClient
    {
        private const string Vid = "VID-ALICE-001";
        private bool _hasVoted;

        public AuthorizationConsumeOutcome ConsumeOutcome { get; set; } = AuthorizationConsumeOutcome.Consumed;

        public int ConsumeGrantCalls { get; private set; }

        public Task<VoterAuthorizationProfile?> GetProfileAsync(CancellationToken cancellationToken)
            => Task.FromResult<VoterAuthorizationProfile?>(new VoterAuthorizationProfile("subject-alice", Vid, DateTimeOffset.UtcNow));

        public Task<ElectionAuthorizationState?> GetElectionStateAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult<ElectionAuthorizationState?>(new ElectionAuthorizationState(
                electionId,
                Vid,
                _hasVoted ? ElectionAuthorizationStatus.AlreadyVoted : ElectionAuthorizationStatus.EligibleNotVoted,
                _hasVoted ? DateTimeOffset.UtcNow : null));

        public Task<AuthorizationGrant?> IssueGrantAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult<AuthorizationGrant?>(new AuthorizationGrant(Guid.NewGuid(), electionId, Vid, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(2)));

        public Task<ConsumeAuthorizationResponse?> ConsumeGrantAsync(string electionId, Guid grantId, string submissionNonce, CancellationToken cancellationToken)
        {
            ConsumeGrantCalls++;
            callOrder.Add("ConsumeGrantAsync");

            if (ConsumeOutcome == AuthorizationConsumeOutcome.Consumed)
            {
                _hasVoted = true;
            }

            return Task.FromResult<ConsumeAuthorizationResponse?>(new ConsumeAuthorizationResponse(
                electionId,
                Vid,
                ConsumeOutcome,
                ConsumeOutcome == AuthorizationConsumeOutcome.Consumed ? DateTimeOffset.UtcNow : null,
                "test outcome"));
        }
    }

    /// <summary>Wraps <see cref="InMemoryBallotSubmissionOutbox"/> to record call order and optionally fail staging.</summary>
    private sealed class RecordingOutbox(List<string> callOrder) : IBallotSubmissionOutbox
    {
        private readonly InMemoryBallotSubmissionOutbox _inner = new();

        public bool ThrowOnStage { get; set; }

        public void Stage(BallotSubmissionPackage package, Guid grantId)
        {
            callOrder.Add("Stage");
            if (ThrowOnStage)
            {
                throw new IOException("Simulated durable staging failure.");
            }

            _inner.Stage(package, grantId);
        }

        public void Store(BallotSubmissionPackage package, string reason) => _inner.Store(package, reason);

        public bool MarkAuthorized(string electionId, string ballotNullifier) => _inner.MarkAuthorized(electionId, ballotNullifier);

        public void Requeue(PendingBallotSubmission pendingSubmission, string reason) => _inner.Requeue(pendingSubmission, reason);

        public bool TryTakeNext(out PendingBallotSubmission pendingSubmission) => _inner.TryTakeNext(out pendingSubmission);

        public bool TryRemove(string electionId, string ballotNullifier, out PendingBallotSubmission removedSubmission)
            => _inner.TryRemove(electionId, ballotNullifier, out removedSubmission);

        public int Compact(DateTimeOffset nowUtc, TimeSpan entryTtl, int maxRetryCount, int maxEntries)
            => _inner.Compact(nowUtc, entryTtl, maxRetryCount, maxEntries);

        public IReadOnlyCollection<PendingBallotSubmission> Snapshot() => _inner.Snapshot();

        public OutboxStatistics GetStatistics(DateTimeOffset nowUtc, TimeSpan entryTtl, int maxRetryCount)
            => _inner.GetStatistics(nowUtc, entryTtl, maxRetryCount);
    }
}