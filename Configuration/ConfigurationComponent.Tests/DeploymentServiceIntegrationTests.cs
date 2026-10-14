using System.Text.Json;
using ConfigurationComponent.Blockchain;
using ConfigurationComponent.Domain;
using ConfigurationComponent.Infrastructure;
using ConfigurationComponent.Repositories;
using ConfigurationComponent.Services;
using ConfigurationComponent.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConfigurationComponent.Tests;

/// <summary>
/// Represents Deployment Service Integration Tests.
/// </summary>
public sealed class DeploymentServiceIntegrationTests
{
    [Fact]
    /// <summary>
    /// Executes Deploy Async_With Valid Draft_Produces Canonical Hash And Persists Deployment.
    /// </summary>
    public async Task DeployAsync_WithValidDraft_ProducesCanonicalHashAndPersistsDeployment()
    {
        var election = TestDataFactory.CreateValidDraftElection();

        var validationService = new ValidationService();
        var metadataGenerator = new MetadataGenerator();
        var metadataHasher = new MetadataHasher();
        var manifestGenerator = new DeploymentManifestGenerator();
        var metadataRepository = new CapturingMetadataRepository("https://metadata.local/election.json");
        var solanaService = new CapturingSolanaService();
        var electionRepository = new InMemoryElectionRepository();
        var manifestRepository = new InMemoryManifestRepository();

        var service = new DeploymentService(
            validationService,
            metadataGenerator,
            metadataHasher,
            manifestGenerator,
            metadataRepository,
            solanaService,
            electionRepository,
            manifestRepository,
            NullLogger<DeploymentService>.Instance);

        var (updatedElection, result) = await service.DeployAsync(election);

        Assert.Equal(ElectionLifecycleState.Deployed, updatedElection.LifecycleState);
        Assert.Equal(DeploymentStatus.Succeeded, updatedElection.DeploymentStatus);
        Assert.Equal("https://metadata.local/election.json", updatedElection.MetadataUri);
        Assert.NotNull(updatedElection.MetadataHash);
        Assert.NotNull(updatedElection.ElectionPda);
        Assert.NotNull(updatedElection.DeploymentSignature);
        Assert.NotNull(updatedElection.DeploymentManifestPath);

        Assert.Equal(updatedElection.MetadataHash, result.MetadataHash);
        Assert.Equal(updatedElection.MetadataUri, result.MetadataUri);

        var saved = await electionRepository.GetByIdAsync(election.Id);
        Assert.NotNull(saved);
        Assert.Equal(ElectionLifecycleState.Deployed, saved!.LifecycleState);

        Assert.NotNull(metadataRepository.UploadedJson);
        Assert.NotNull(solanaService.LastMetadataHash);
        Assert.NotNull(manifestRepository.LastSavedJson);
        Assert.Equal(solanaService.LastMetadataHash, updatedElection.MetadataHash);

        using var finalDoc = JsonDocument.Parse(metadataRepository.UploadedJson!);
        var finalRoot = finalDoc.RootElement;

        var ts = finalRoot.GetProperty("verification").GetProperty("creationTimestamp").GetInt64();
        var hashInFinalMetadata = finalRoot.GetProperty("verification").GetProperty("metadataHash").GetString();
        Assert.Equal(updatedElection.MetadataHash, hashInFinalMetadata);

        var canonicalForHash = metadataGenerator.BuildMetadata(election, $"pda_{election.Id:N}", string.Empty, ts);
        var canonicalJson = metadataGenerator.Serialize(canonicalForHash);
        var expectedHash = metadataHasher.ComputeSha256Hex(canonicalJson);

        Assert.Equal(expectedHash, updatedElection.MetadataHash);
    }

    [Fact]
    /// <summary>
    /// Executes Deploy Async_When Validation Fails_Does Not Upload Or Deploy.
    /// </summary>
    public async Task DeployAsync_WhenValidationFails_DoesNotUploadOrDeploy()
    {
        var invalidElection = TestDataFactory.CreateValidDraftElection() with { Name = " " };

        var validationService = new ValidationService();
        var metadataGenerator = new MetadataGenerator();
        var metadataHasher = new MetadataHasher();
        var manifestGenerator = new DeploymentManifestGenerator();
        var metadataRepository = new CapturingMetadataRepository("https://metadata.local/election.json");
        var solanaService = new CapturingSolanaService();
        var electionRepository = new InMemoryElectionRepository();
        var manifestRepository = new InMemoryManifestRepository();

        var service = new DeploymentService(
            validationService,
            metadataGenerator,
            metadataHasher,
            manifestGenerator,
            metadataRepository,
            solanaService,
            electionRepository,
            manifestRepository,
            NullLogger<DeploymentService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeployAsync(invalidElection));

        Assert.Null(metadataRepository.UploadedJson);
        Assert.Null(solanaService.LastMetadataHash);
        Assert.Null(manifestRepository.LastSavedJson);
        Assert.Empty(await electionRepository.GetAllAsync());
    }

    [Fact]
    /// <summary>
    /// Executes Deploy Async_When Metadata Upload Throws_Propagates And Does Not Call Solana Or Persist.
    /// </summary>
    public async Task DeployAsync_WhenMetadataUploadThrows_PropagatesAndDoesNotCallSolanaOrPersist()
    {
        var election = TestDataFactory.CreateValidDraftElection();

        var validationService = new ValidationService();
        var metadataGenerator = new MetadataGenerator();
        var metadataHasher = new MetadataHasher();
        var manifestGenerator = new DeploymentManifestGenerator();
        var metadataRepository = new ThrowingMetadataRepository(new HttpRequestException("metadata upload failed"));
        var solanaService = new CapturingSolanaService();
        var electionRepository = new InMemoryElectionRepository();
        var manifestRepository = new InMemoryManifestRepository();

        var service = new DeploymentService(
            validationService,
            metadataGenerator,
            metadataHasher,
            manifestGenerator,
            metadataRepository,
            solanaService,
            electionRepository,
            manifestRepository,
            NullLogger<DeploymentService>.Instance);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => service.DeployAsync(election));

        Assert.Equal("metadata upload failed", ex.Message);
        Assert.Null(solanaService.LastMetadataHash);
        Assert.Null(manifestRepository.LastSavedJson);
        Assert.Empty(await electionRepository.GetAllAsync());
    }

    [Fact]
    /// <summary>
    /// Executes Deploy Async_When Solana Deploy Throws_Propagates And Does Not Persist.
    /// </summary>
    public async Task DeployAsync_WhenSolanaDeployThrows_PropagatesAndDoesNotPersist()
    {
        var election = TestDataFactory.CreateValidDraftElection();

        var validationService = new ValidationService();
        var metadataGenerator = new MetadataGenerator();
        var metadataHasher = new MetadataHasher();
        var manifestGenerator = new DeploymentManifestGenerator();
        var metadataRepository = new CapturingMetadataRepository("https://metadata.local/election.json");
        var solanaService = new ThrowingSolanaService(new InvalidOperationException("solana deploy failed"));
        var electionRepository = new InMemoryElectionRepository();
        var manifestRepository = new InMemoryManifestRepository();

        var service = new DeploymentService(
            validationService,
            metadataGenerator,
            metadataHasher,
            manifestGenerator,
            metadataRepository,
            solanaService,
            electionRepository,
            manifestRepository,
            NullLogger<DeploymentService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeployAsync(election));

        Assert.Equal("solana deploy failed", ex.Message);
        Assert.NotNull(metadataRepository.UploadedJson);
        Assert.Null(manifestRepository.LastSavedJson);
        Assert.Empty(await electionRepository.GetAllAsync());
    }

    private sealed class InMemoryManifestRepository : IDeploymentManifestRepository
    {
        public string? LastSavedPath { get; private set; }

        public string? LastSavedJson { get; private set; }

        /// <summary>
        /// Executes Save Async.
        /// </summary>
        public Task<string> SaveAsync(string electionId, string manifestJson, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            LastSavedPath = $"mem://{electionId}.manifest.json";
            LastSavedJson = manifestJson;
            return Task.FromResult(LastSavedPath);
        }
    }

    private sealed class CapturingMetadataRepository(string metadataUri) : IMetadataRepository
    {
        public string? UploadedJson { get; private set; }

        /// <summary>
        /// Executes Upload Metadata Async.
        /// </summary>
        public Task<string> UploadMetadataAsync(string endpoint, string metadataJson, string electionId, CancellationToken cancellationToken = default)
        {
            _ = endpoint;
            _ = electionId;
            _ = cancellationToken;

            UploadedJson = metadataJson;
            return Task.FromResult(metadataUri);
        }
    }

    private sealed class ThrowingMetadataRepository(Exception exception) : IMetadataRepository
    {
        /// <summary>
        /// Executes Upload Metadata Async.
        /// </summary>
        public Task<string> UploadMetadataAsync(string endpoint, string metadataJson, string electionId, CancellationToken cancellationToken = default)
        {
            _ = endpoint;
            _ = metadataJson;
            _ = electionId;
            _ = cancellationToken;

            throw exception;
        }
    }

    private sealed class CapturingSolanaService : ISolanaService
    {
        public string? LastMetadataHash { get; private set; }

        /// <summary>
        /// Executes Deploy Election Async.
        /// </summary>
        public Task<DeploymentResult> DeployElectionAsync(ElectionConfiguration election, string metadataHash, string metadataUri, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            LastMetadataHash = metadataHash;
            return Task.FromResult(new DeploymentResult(
                ProgramId: election.DeploymentSettings.ProgramId,
                ElectionPda: $"pda_{election.Id:N}",
                MetadataUri: metadataUri,
                TransactionSignature: "tx_test_signature",
                MetadataHash: metadataHash));
        }

        /// <summary>
        /// Executes Initialize Election Async.
        /// </summary>
        public Task InitializeElectionAsync(ElectionConfiguration election, string metadataHash, CancellationToken cancellationToken = default)
        {
            _ = election;
            _ = metadataHash;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Executes Activate Election Async.
        /// </summary>
        public Task ActivateElectionAsync(Guid electionId, CancellationToken cancellationToken = default)
        {
            _ = electionId;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Executes Close Election Async.
        /// </summary>
        public Task CloseElectionAsync(Guid electionId, CancellationToken cancellationToken = default)
        {
            _ = electionId;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Executes Get Election Status Async.
        /// </summary>
        public Task<ElectionLifecycleState> GetElectionStatusAsync(Guid electionId, CancellationToken cancellationToken = default)
        {
            _ = electionId;
            _ = cancellationToken;
            return Task.FromResult(ElectionLifecycleState.Deployed);
        }
    }

    private sealed class ThrowingSolanaService(Exception exception) : ISolanaService
    {
        /// <summary>
        /// Executes Deploy Election Async.
        /// </summary>
        public Task<DeploymentResult> DeployElectionAsync(ElectionConfiguration election, string metadataHash, string metadataUri, CancellationToken cancellationToken = default)
        {
            _ = election;
            _ = metadataHash;
            _ = metadataUri;
            _ = cancellationToken;

            throw exception;
        }

        /// <summary>
        /// Executes Initialize Election Async.
        /// </summary>
        public Task InitializeElectionAsync(ElectionConfiguration election, string metadataHash, CancellationToken cancellationToken = default)
        {
            _ = election;
            _ = metadataHash;
            _ = cancellationToken;
            throw exception;
        }

        /// <summary>
        /// Executes Activate Election Async.
        /// </summary>
        public Task ActivateElectionAsync(Guid electionId, CancellationToken cancellationToken = default)
        {
            _ = electionId;
            _ = cancellationToken;
            throw exception;
        }

        /// <summary>
        /// Executes Close Election Async.
        /// </summary>
        public Task CloseElectionAsync(Guid electionId, CancellationToken cancellationToken = default)
        {
            _ = electionId;
            _ = cancellationToken;
            throw exception;
        }

        /// <summary>
        /// Executes Get Election Status Async.
        /// </summary>
        public Task<ElectionLifecycleState> GetElectionStatusAsync(Guid electionId, CancellationToken cancellationToken = default)
        {
            _ = electionId;
            _ = cancellationToken;
            throw exception;
        }
    }

    private sealed class InMemoryElectionRepository : IElectionRepository
    {
        private readonly Dictionary<Guid, ElectionConfiguration> _store = [];

        /// <summary>
        /// Executes Get All Async.
        /// </summary>
        public Task<IReadOnlyList<ElectionConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult<IReadOnlyList<ElectionConfiguration>>(_store.Values.ToList());
        }

        /// <summary>
        /// Executes Get By Id Async.
        /// </summary>
        public Task<ElectionConfiguration?> GetByIdAsync(Guid electionId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _store.TryGetValue(electionId, out var election);
            return Task.FromResult(election);
        }

        /// <summary>
        /// Executes Save Async.
        /// </summary>
        public Task SaveAsync(ElectionConfiguration election, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _store[election.Id] = election;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Executes Delete Async.
        /// </summary>
        public Task DeleteAsync(Guid electionId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _store.Remove(electionId);
            return Task.CompletedTask;
        }
    }
}
