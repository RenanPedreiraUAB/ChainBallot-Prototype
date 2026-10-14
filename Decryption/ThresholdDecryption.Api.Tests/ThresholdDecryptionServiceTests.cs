using Microsoft.Extensions.Options;
using ThresholdDecryption.Api.Application;
using ThresholdDecryption.Api.Domain;
using ThresholdDecryption.Api.Services;

namespace ThresholdDecryption.Api.Tests;

public sealed class ThresholdDecryptionServiceTests
{
    [Fact]
    public async Task FinalizeDecryptionAsync_Completes_WhenApprovalsAndSharesMeetThreshold()
    {
        var repository = new InMemoryRepository();
        var sut = BuildService(repository);

        await sut.ProposeCeremonyAsync("election-1", 2, 1, "operator", CancellationToken.None);
        await sut.ApproveCeremonyAsync("election-1", "governor-a", "ok", CancellationToken.None);
        await sut.SubmitShareAsync("election-1", "trustee-a", "share-a", CancellationToken.None);
        await sut.SubmitShareAsync("election-1", "trustee-b", "share-b", CancellationToken.None);

        var finalized = await sut.FinalizeDecryptionAsync(
            "election-1",
            new Dictionary<string, long> { ["cand-a"] = 10, ["cand-b"] = 7 },
            "operator",
            CancellationToken.None);

        Assert.NotNull(finalized);
        Assert.Equal(CeremonyStatus.Completed, finalized!.Status);
        Assert.NotNull(finalized.ResultDigest);
        Assert.NotNull(finalized.DecryptedTallies);
    }

    [Fact]
    public async Task SubmitShareAsync_DoesNotCountShare_BeforeApprovalsAreSatisfied()
    {
        var repository = new InMemoryRepository();
        var sut = BuildService(repository);

        await sut.ProposeCeremonyAsync("election-2", 2, 2, "operator", CancellationToken.None);
        var updated = await sut.SubmitShareAsync("election-2", "trustee-a", "share-a", CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Empty(updated!.Shares);
        Assert.Contains(updated.Events, e => e.EventType == "share-rejected");
    }

    [Fact]
    public async Task PublishFinalTallyAsync_AttachesPublicationReference_WhenCeremonyCompleted()
    {
        var repository = new InMemoryRepository();
        var sut = BuildService(repository);

        await sut.ProposeCeremonyAsync("election-3", 1, 1, "operator", CancellationToken.None);
        await sut.ApproveCeremonyAsync("election-3", "governor-a", "ok", CancellationToken.None);
        await sut.SubmitShareAsync("election-3", "trustee-a", "share-a", CancellationToken.None);
        await sut.FinalizeDecryptionAsync("election-3", new Dictionary<string, long> { ["cand-a"] = 3 }, "operator", CancellationToken.None);

        var published = await sut.PublishFinalTallyAsync("election-3", "operator", CancellationToken.None);

        Assert.NotNull(published);
        Assert.NotNull(published!.Publication);
        Assert.StartsWith("threshold://", published.Publication!.Reference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProposeCeremonyAsync_RejectedWhenCanonicalStateIsNotClosed()
    {
        var repository = new InMemoryRepository();
        var canonicalClient = new FakeCanonicalElectionStateClient(isFinalized: false);
        var sut = BuildService(repository, canonicalClient);

        var ceremony = await sut.ProposeCeremonyAsync("election-open", 1, 1, "operator", CancellationToken.None);

        Assert.Equal(CeremonyStatus.Failed, ceremony.Status);
        Assert.Contains(ceremony.Events, e => e.EventType == "closure-check-rejected");
        Assert.Equal(1, canonicalClient.CallCount);
    }

    [Fact]
    public async Task ProposeCeremonyAsync_AcceptedWhenCanonicalStateIsClosed()
    {
        var repository = new InMemoryRepository();
        var canonicalClient = new FakeCanonicalElectionStateClient(isFinalized: true);
        var sut = BuildService(repository, canonicalClient);

        var ceremony = await sut.ProposeCeremonyAsync("election-closed", 1, 1, "operator", CancellationToken.None);

        Assert.Equal(CeremonyStatus.Proposed, ceremony.Status);
        Assert.Contains(ceremony.Events, e => e.EventType == "proposed");
    }

    [Fact]
    public async Task ProposeCeremonyAsync_RejectedWhenCanonicalStateEndpointIsUnavailable()
    {
        var repository = new InMemoryRepository();
        var sut = BuildService(repository, new UnavailableCanonicalElectionStateClient());

        var ceremony = await sut.ProposeCeremonyAsync("election-unavailable", 1, 1, "operator", CancellationToken.None);

        Assert.Equal(CeremonyStatus.Failed, ceremony.Status);
        Assert.Contains(ceremony.Events, e => e.EventType == "closure-check-rejected");
    }

    [Fact]
    public async Task FinalizeDecryptionAsync_RejectedWhenCanonicalStateIsNotClosed()
    {
        var repository = new InMemoryRepository();
        var canonicalClient = new FakeCanonicalElectionStateClient(isFinalized: true);
        var sut = BuildService(repository, canonicalClient);

        await sut.ProposeCeremonyAsync("election-4", 1, 1, "operator", CancellationToken.None);
        await sut.ApproveCeremonyAsync("election-4", "governor-a", "ok", CancellationToken.None);
        await sut.SubmitShareAsync("election-4", "trustee-a", "share-a", CancellationToken.None);

        // Canonical state regresses (or was never actually closed) between proposal and finalisation.
        canonicalClient.IsFinalizedOverride = false;

        var finalized = await sut.FinalizeDecryptionAsync(
            "election-4",
            new Dictionary<string, long> { ["cand-a"] = 1 },
            "operator",
            CancellationToken.None);

        Assert.NotNull(finalized);
        Assert.NotEqual(CeremonyStatus.Completed, finalized!.Status);
        Assert.Contains(finalized.Events, e => e.EventType == "closure-check-rejected");
        Assert.Null(finalized.ResultDigest);
    }

    [Fact]
    public async Task ProposeCeremonyAsync_CalledTwice_ReturnsSameCeremonyId()
    {
        var repository = new InMemoryRepository();
        var sut = BuildService(repository);

        var first = await sut.ProposeCeremonyAsync("election-5", 1, 1, "operator", CancellationToken.None);
        var second = await sut.ProposeCeremonyAsync("election-5", 1, 1, "operator", CancellationToken.None);

        Assert.Equal(first.CeremonyId, second.CeremonyId);
    }

    [Fact]
    public async Task FinalizeDecryptionAsync_CalledTwice_PreservesOriginalResultDigest()
    {
        var repository = new InMemoryRepository();
        var sut = BuildService(repository);

        await sut.ProposeCeremonyAsync("election-6", 1, 1, "operator", CancellationToken.None);
        await sut.ApproveCeremonyAsync("election-6", "governor-a", "ok", CancellationToken.None);
        await sut.SubmitShareAsync("election-6", "trustee-a", "share-a", CancellationToken.None);

        var first = await sut.FinalizeDecryptionAsync("election-6", new Dictionary<string, long> { ["cand-a"] = 5 }, "operator", CancellationToken.None);
        var second = await sut.FinalizeDecryptionAsync("election-6", new Dictionary<string, long> { ["cand-a"] = 999 }, "operator", CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(CeremonyStatus.Completed, second!.Status);
        Assert.Equal(first!.ResultDigest, second.ResultDigest);
    }

    [Fact]
    public async Task PublishFinalTallyAsync_CalledTwice_ReturnsSameReference()
    {
        var repository = new InMemoryRepository();
        var sut = BuildService(repository);

        await sut.ProposeCeremonyAsync("election-7", 1, 1, "operator", CancellationToken.None);
        await sut.ApproveCeremonyAsync("election-7", "governor-a", "ok", CancellationToken.None);
        await sut.SubmitShareAsync("election-7", "trustee-a", "share-a", CancellationToken.None);
        await sut.FinalizeDecryptionAsync("election-7", new Dictionary<string, long> { ["cand-a"] = 2 }, "operator", CancellationToken.None);

        var first = await sut.PublishFinalTallyAsync("election-7", "operator", CancellationToken.None);
        var second = await sut.PublishFinalTallyAsync("election-7", "operator", CancellationToken.None);

        Assert.NotNull(first?.Publication);
        Assert.NotNull(second?.Publication);
        Assert.Equal(first!.Publication!.Reference, second!.Publication!.Reference);
        Assert.Equal(first.Publication.PublicationId, second.Publication.PublicationId);
    }

    private static ThresholdDecryptionService BuildService(IThresholdCeremonyRepository repository, ICanonicalElectionStateClient? canonicalElectionStateClient = null)
    {
        var options = Options.Create(new ThresholdOptions
        {
            DefaultRequiredShares = 3,
            DefaultRequiredApprovals = 2,
            AllowedTrustees = ["trustee-a", "trustee-b", "trustee-c"],
            GovernanceApprovers = ["governor-a", "governor-b"]
        });

        return new ThresholdDecryptionService(repository, canonicalElectionStateClient ?? new FakeCanonicalElectionStateClient(isFinalized: true), options);
    }

    private sealed class FakeCanonicalElectionStateClient(bool isFinalized) : ICanonicalElectionStateClient
    {
        public int CallCount { get; private set; }

        public bool IsFinalizedOverride { get; set; } = isFinalized;

        public Task<CanonicalElectionStateDto?> GetElectionStateAsync(string electionId, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<CanonicalElectionStateDto?>(
                new CanonicalElectionStateDto(electionId, IsFinalizedOverride, "Test", DateTimeOffset.UtcNow, null));
        }
    }

    private sealed class UnavailableCanonicalElectionStateClient : ICanonicalElectionStateClient
    {
        public Task<CanonicalElectionStateDto?> GetElectionStateAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult<CanonicalElectionStateDto?>(null);
    }

    private sealed class InMemoryRepository : IThresholdCeremonyRepository
    {
        private readonly List<ThresholdCeremony> _items = [];

        public Task<ThresholdCeremony> SaveAsync(ThresholdCeremony ceremony, CancellationToken cancellationToken)
        {
            _items.RemoveAll(i => string.Equals(i.CeremonyId, ceremony.CeremonyId, StringComparison.OrdinalIgnoreCase));
            _items.Add(ceremony);
            return Task.FromResult(ceremony);
        }

        public Task<ThresholdCeremony?> GetByIdAsync(string ceremonyId, CancellationToken cancellationToken)
            => Task.FromResult(_items.FirstOrDefault(i => string.Equals(i.CeremonyId, ceremonyId, StringComparison.OrdinalIgnoreCase)));

        public Task<ThresholdCeremony?> GetByElectionAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult(_items.Where(i => string.Equals(i.ElectionId, electionId, StringComparison.OrdinalIgnoreCase)).OrderByDescending(i => i.OpenedAtUtc).FirstOrDefault());

        public Task<IReadOnlyList<ThresholdCeremony>> ListByElectionAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ThresholdCeremony>>(_items.Where(i => string.Equals(i.ElectionId, electionId, StringComparison.OrdinalIgnoreCase)).OrderByDescending(i => i.OpenedAtUtc).ToArray());
    }
}
