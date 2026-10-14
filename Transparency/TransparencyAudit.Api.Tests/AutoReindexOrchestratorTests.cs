using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ChainBallot.PublicContracts;
using TransparencyAudit.Api.Application;
using TransparencyAudit.Api.Domain;
using TransparencyAudit.Api.Services;

namespace TransparencyAudit.Api.Tests;

public sealed class AutoReindexOrchestratorTests
{
    [Fact]
    public async Task RunOnceAsync_ReindexesConfiguredElectionIds_WhenEnabled()
    {
        var management = new RecordingProjectionManagementService();
        var options = Options.Create(new TransparencyOptions
        {
            AutoReindexEnabled = true,
            AutoReindexElectionIds = new[] { "e1", "e2" }
        });

        var orchestrator = new AutoReindexOrchestrator(management, options, NullLogger<AutoReindexOrchestrator>.Instance);

        await orchestrator.RunOnceAsync(CancellationToken.None);

        Assert.Equal(new[] { "e1", "e2" }, management.ReindexedElectionIds);
    }

    [Fact]
    public async Task RunOnceAsync_DoesNothing_WhenDisabled()
    {
        var management = new RecordingProjectionManagementService();
        var options = Options.Create(new TransparencyOptions
        {
            AutoReindexEnabled = false,
            AutoReindexElectionIds = new[] { "e1" }
        });

        var orchestrator = new AutoReindexOrchestrator(management, options, NullLogger<AutoReindexOrchestrator>.Instance);

        await orchestrator.RunOnceAsync(CancellationToken.None);

        Assert.Empty(management.ReindexedElectionIds);
    }

    private sealed class RecordingProjectionManagementService : IProjectionManagementService
    {
        public List<string> ReindexedElectionIds { get; } = new();

        public Task<ElectionProjection> IngestEvidenceAsync(ProjectionEvidenceIngestion evidence, CancellationToken cancellationToken)
            => Task.FromResult(new ElectionProjection(
                evidence.ElectionId,
                evidence.LifecycleState,
                evidence.SettlementReference,
                evidence.CandidateTallies,
                evidence.ResultDigest,
                DateTimeOffset.UtcNow,
                Array.Empty<VidEntry>()));

        public Task<ElectionProjection?> ReindexElectionAsync(string electionId, CancellationToken cancellationToken)
        {
            ReindexedElectionIds.Add(electionId);
            return Task.FromResult<ElectionProjection?>(new ElectionProjection(
                electionId,
                "Finalized",
                new SettlementReference("Localnet", "p", electionId, "tx", 1UL, DateTimeOffset.UtcNow, "finalized"),
                new Dictionary<string, long>(),
                "digest",
                DateTimeOffset.UtcNow,
                Array.Empty<VidEntry>()));
        }

        public Task<ElectionProjection?> MarkStaleAsync(string electionId, string staleReason, CancellationToken cancellationToken)
            => Task.FromResult<ElectionProjection?>(null);
    }
}
