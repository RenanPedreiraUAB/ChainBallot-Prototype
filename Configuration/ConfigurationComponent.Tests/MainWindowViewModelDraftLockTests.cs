using ConfigurationComponent.Application;
using ConfigurationComponent.Domain;
using ConfigurationComponent.Services;
using ConfigurationComponent.Tests.Helpers;
using ConfigurationComponent.ViewModels;

namespace ConfigurationComponent.Tests;

/// <summary>
/// Represents Main Window View Model Draft Lock Tests.
/// </summary>
public sealed class MainWindowViewModelDraftLockTests
{
    [Fact]
    /// <summary>
    /// Executes Open Election_With No Selection_Shows Validation Error.
    /// </summary>
    public void OpenElection_WithNoSelection_ShowsValidationError()
    {
        var viewModel = CreateViewModel();

        viewModel.OpenElection();

        Assert.Equal("Select an election first.", viewModel.ValidationErrors);
    }

    [Fact]
    /// <summary>
    /// Executes Open Election_With Selection_Moves To Editor Tab.
    /// </summary>
    public void OpenElection_WithSelection_MovesToEditorTab()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedElection = TestDataFactory.CreateValidDraftElection();

        viewModel.OpenElection();

        Assert.Equal(1, viewModel.SelectedTabIndex);
    }

    [Fact]
    /// <summary>
    /// Executes Selecting Deployed Election_Disables Draft Editing.
    /// </summary>
    public void SelectingDeployedElection_DisablesDraftEditing()
    {
        var viewModel = CreateViewModel();
        var deployed = TestDataFactory.CreateValidDraftElection() with { LifecycleState = ElectionLifecycleState.Deployed };

        viewModel.SelectedElection = deployed;

        Assert.False(viewModel.CanEditDraft);
    }

    [Fact]
    /// <summary>
    /// Executes Add Candidate_When Not Draft_Does Not Change Candidates.
    /// </summary>
    public void AddCandidate_WhenNotDraft_DoesNotChangeCandidates()
    {
        var viewModel = CreateViewModel();
        var deployed = TestDataFactory.CreateValidDraftElection() with { LifecycleState = ElectionLifecycleState.Deployed };
        viewModel.SelectedElection = deployed;

        var before = viewModel.Candidates.Count;
        viewModel.NewCandidateName = "Charlie";
        viewModel.AddCandidate();

        Assert.Equal(before, viewModel.Candidates.Count);
        Assert.Equal("Only DRAFT elections can be modified.", viewModel.ValidationErrors);
    }

    [Fact]
    /// <summary>
    /// Executes Save Draft Async_When Not Draft_Does Not Call Election Service.
    /// </summary>
    public async Task SaveDraftAsync_WhenNotDraft_DoesNotCallElectionService()
    {
        var electionService = new TrackingElectionService();
        var viewModel = CreateViewModel(electionService: electionService);

        var deployed = TestDataFactory.CreateValidDraftElection() with { LifecycleState = ElectionLifecycleState.Deployed };
        viewModel.SelectedElection = deployed;

        await viewModel.SaveDraftAsync();

        Assert.Equal(0, electionService.SaveDraftCallCount);
        Assert.Equal("Only DRAFT elections can be modified.", viewModel.ValidationErrors);
    }

    [Fact]
    /// <summary>
    /// Executes Deploy Async_When Not Draft_Does Not Call Deployment Service.
    /// </summary>
    public async Task DeployAsync_WhenNotDraft_DoesNotCallDeploymentService()
    {
        var deploymentService = new TrackingDeploymentService();
        var viewModel = CreateViewModel(deploymentService: deploymentService);

        var deployed = TestDataFactory.CreateValidDraftElection() with { LifecycleState = ElectionLifecycleState.Deployed };
        viewModel.SelectedElection = deployed;

        await viewModel.DeployAsync();

        Assert.Equal(0, deploymentService.CallCount);
        Assert.Equal("Only DRAFT elections can be modified.", viewModel.ValidationErrors);
    }

    private static MainWindowViewModel CreateViewModel(
        IElectionService? electionService = null,
        IDeploymentService? deploymentService = null)
    {
        return new MainWindowViewModel(
            electionService ?? new TrackingElectionService(),
            new CandidateService(),
            new RecommendationService(),
            new MetadataGenerator(),
            new MetadataHasher(),
            new ValidationService(),
            deploymentService ?? new TrackingDeploymentService(),
            new ProvisioningDefaults());
    }

    private sealed class TrackingElectionService : IElectionService
    {
        public int SaveDraftCallCount { get; private set; }

        /// <summary>
        /// Executes Get All Async.
        /// </summary>
        public Task<IReadOnlyList<ElectionConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult<IReadOnlyList<ElectionConfiguration>>([]);
        }

        /// <summary>
        /// Executes Create Draft Async.
        /// </summary>
        public Task<ElectionConfiguration> CreateDraftAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(TestDataFactory.CreateValidDraftElection());
        }

        /// <summary>
        /// Executes Save Draft Async.
        /// </summary>
        public Task<ElectionConfiguration> SaveDraftAsync(ElectionConfiguration election, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            SaveDraftCallCount++;
            return Task.FromResult(election);
        }

        /// <summary>
        /// Executes Delete Draft Async.
        /// </summary>
        public Task DeleteDraftAsync(Guid electionId, CancellationToken cancellationToken = default)
        {
            _ = electionId;
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingDeploymentService : IDeploymentService
    {
        public int CallCount { get; private set; }

        /// <summary>
        /// Executes Deploy Async.
        /// </summary>
        public Task<(ElectionConfiguration UpdatedElection, DeploymentResult Result)> DeployAsync(
            ElectionConfiguration election,
            CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            CallCount++;

            return Task.FromResult((
                election,
                new DeploymentResult(
                    ProgramId: election.DeploymentSettings.ProgramId,
                    ElectionPda: $"pda_{election.Id:N}",
                    MetadataUri: "metadata://example",
                    TransactionSignature: "tx_example",
                    MetadataHash: "hash_example")));
        }
    }
}
