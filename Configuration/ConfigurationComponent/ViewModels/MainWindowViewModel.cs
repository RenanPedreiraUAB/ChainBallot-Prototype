using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConfigurationComponent.Application;
using ConfigurationComponent.Domain;
using ConfigurationComponent.Services;

namespace ConfigurationComponent.ViewModels;

/// <summary>
/// Represents Main Window View Model.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IElectionService _electionService;
    private readonly ICandidateService _candidateService;
    private readonly IRecommendationService _recommendationService;
    private readonly IMetadataGenerator _metadataGenerator;
    private readonly IMetadataHasher _metadataHasher;
    private readonly IValidationService _validationService;
    private readonly IDeploymentService _deploymentService;

    [ObservableProperty]
    private ObservableCollection<ElectionConfiguration> elections = [];

    [ObservableProperty]
    private ElectionConfiguration? selectedElection;

    [ObservableProperty]
    private string electionName = string.Empty;

    [ObservableProperty]
    private string electionDescription = string.Empty;

    [ObservableProperty]
    private DateTimeOffset openingDate = DateTimeOffset.UtcNow.AddDays(1);

    [ObservableProperty]
    private DateTimeOffset closingDate = DateTimeOffset.UtcNow.AddDays(2);

    [ObservableProperty]
    private ElectionType selectedElectionType = ElectionType.SingleChoice;

    [ObservableProperty]
    private SolanaNetwork selectedNetwork = SolanaNetwork.Devnet;

    [ObservableProperty]
    private string programId = "DefaultProgramId";

    [ObservableProperty]
    private string metadataEndpoint = "https://metadata.example/api/";

    [ObservableProperty]
    private int expectedVoters = 1000;

    [ObservableProperty]
    private int peakParticipationPercent = 25;

    [ObservableProperty]
    private int electionDurationHours = 24;

    [ObservableProperty]
    private string programVersion = "1.0.0";

    [ObservableProperty]
    private ObservableCollection<Candidate> candidates = [];

    [ObservableProperty]
    private string newCandidateName = string.Empty;

    [ObservableProperty]
    private string newCandidateParty = string.Empty;

    [ObservableProperty]
    private string newCandidateDescription = string.Empty;

    [ObservableProperty]
    private string newCandidateImage = string.Empty;

    [ObservableProperty]
    private Candidate? selectedCandidate;

    [ObservableProperty]
    private string metadataPreview = string.Empty;

    [ObservableProperty]
    private string metadataHash = string.Empty;

    [ObservableProperty]
    private string recommendationSummary = string.Empty;

    [ObservableProperty]
    private string deploymentSummary = string.Empty;

    [ObservableProperty]
    private string validationErrors = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool canEditDraft;

    [ObservableProperty]
    private int selectedTabIndex;

    public IReadOnlyList<ElectionType> ElectionTypes { get; } = Enum.GetValues<ElectionType>();

    public IReadOnlyList<SolanaNetwork> Networks { get; } = Enum.GetValues<SolanaNetwork>();

    public MainWindowViewModel(
        IElectionService electionService,
        ICandidateService candidateService,
        IRecommendationService recommendationService,
        IMetadataGenerator metadataGenerator,
        IMetadataHasher metadataHasher,
        IValidationService validationService,
        IDeploymentService deploymentService,
        ProvisioningDefaults defaults)
    {
        _electionService = electionService;
        _candidateService = candidateService;
        _recommendationService = recommendationService;
        _metadataGenerator = metadataGenerator;
        _metadataHasher = metadataHasher;
        _validationService = validationService;
        _deploymentService = deploymentService;

        OpeningDate = DateTimeOffset.UtcNow.AddDays(Math.Max(1, defaults.OpeningOffsetDays));
        ClosingDate = DateTimeOffset.UtcNow.AddDays(Math.Max(2, defaults.ClosingOffsetDays));
        SelectedNetwork = defaults.Network;
        ProgramId = defaults.ProgramId;
        MetadataEndpoint = defaults.MetadataEndpoint;
        ExpectedVoters = defaults.ExpectedVoters;
        PeakParticipationPercent = defaults.PeakParticipationPercent;
        ElectionDurationHours = defaults.ElectionDurationHours;
        ProgramVersion = defaults.ProgramVersion;
    }

    partial void OnSelectedElectionChanged(ElectionConfiguration? value)
    {
        if (value is null)
        {
            CanEditDraft = false;
            return;
        }

        CanEditDraft = value.IsDraft;
        LoadElectionToEditor(value);
    }

    [RelayCommand]
    /// <summary>
    /// Executes Load Async.
    /// </summary>
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var loaded = await _electionService.GetAllAsync();
            Elections = new ObservableCollection<ElectionConfiguration>(loaded);
            SelectedElection = Elections.FirstOrDefault();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    /// <summary>
    /// Executes Create Election Async.
    /// </summary>
    public async Task CreateElectionAsync()
    {
        IsBusy = true;
        try
        {
            var election = await _electionService.CreateDraftAsync();
            Elections.Insert(0, election);
            SelectedElection = election;
            SelectedTabIndex = 1;
            ValidationErrors = string.Empty;
            DeploymentSummary = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    /// <summary>
    /// Executes Delete Draft Async.
    /// </summary>
    public async Task DeleteDraftAsync()
    {
        if (SelectedElection is null)
        {
            return;
        }

        if (!SelectedElection.IsDraft)
        {
            ValidationErrors = "Only DRAFT elections can be deleted.";
            return;
        }

        IsBusy = true;
        try
        {
            await _electionService.DeleteDraftAsync(SelectedElection.Id);
            var removedId = SelectedElection.Id;
            var updated = Elections.Where(e => e.Id != removedId).ToList();
            Elections = new ObservableCollection<ElectionConfiguration>(updated);
            SelectedElection = Elections.FirstOrDefault();
            SelectedTabIndex = 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    /// <summary>
    /// Executes Save Draft Async.
    /// </summary>
    public async Task SaveDraftAsync()
    {
        if (SelectedElection is null)
        {
            return;
        }

        if (!CanEditDraft)
        {
            ValidationErrors = "Only DRAFT elections can be modified.";
            return;
        }

        var updated = BuildElectionFromEditor(SelectedElection);
        var validation = _validationService.ValidateForDraftSave(updated);
        if (!validation.IsValid)
        {
            ValidationErrors = string.Join(Environment.NewLine, validation.Errors);
            return;
        }

        IsBusy = true;
        try
        {
            var saved = await _electionService.SaveDraftAsync(updated);
            ReplaceElection(saved);
            SelectedElection = saved;
            ValidationErrors = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    /// <summary>
    /// Switches to the Candidates tab so the next step is a single click away.
    /// </summary>
    public void GoToCandidateEditor()
    {
        SelectedTabIndex = 2;
    }

    [RelayCommand]
    /// <summary>
    /// Executes Add Candidate.
    /// </summary>
    public void AddCandidate()
    {        if (!CanEditDraft)
        {
            ValidationErrors = "Only DRAFT elections can be modified.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewCandidateName))
        {
            ValidationErrors = "Candidate name is required.";
            return;
        }

        var candidate = _candidateService.CreateCandidate(
            Candidates,
            NewCandidateName,
            NewCandidateParty,
            NewCandidateDescription,
            NewCandidateImage);

        Candidates.Add(candidate);

        NewCandidateName = string.Empty;
        NewCandidateParty = string.Empty;
        NewCandidateDescription = string.Empty;
        NewCandidateImage = string.Empty;
    }

    [RelayCommand]
    /// <summary>
    /// Executes Remove Candidate.
    /// </summary>
    public void RemoveCandidate()
    {
        if (!CanEditDraft)
        {
            ValidationErrors = "Only DRAFT elections can be modified.";
            return;
        }

        if (SelectedCandidate is null)
        {
            return;
        }

        var updated = _candidateService.RemoveCandidate(Candidates, SelectedCandidate.Id);
        Candidates = new ObservableCollection<Candidate>(updated);
        SelectedCandidate = null;
    }

    [RelayCommand]
    /// <summary>
    /// Executes Generate Recommendation.
    /// </summary>
    public void GenerateRecommendation()
    {
        var recommendation = _recommendationService.Recommend(ExpectedVoters, ElectionDurationHours, PeakParticipationPercent);

        RecommendationSummary =
            $"Consumers: {recommendation.RecommendedConsumerNodes}{Environment.NewLine}" +
            $"Kafka Partitions: {recommendation.RecommendedKafkaPartitions}{Environment.NewLine}" +
            $"Batch Size: {recommendation.RecommendedBatchSize}{Environment.NewLine}" +
            $"Settlement Interval: {recommendation.RecommendedSettlementInterval.TotalSeconds:0}s{Environment.NewLine}" +
            $"Estimated Cost: {recommendation.EstimatedBlockchainCostSol} SOL";
    }

    [RelayCommand]
    /// <summary>
    /// Executes Generate Metadata Preview.
    /// </summary>
    public void GenerateMetadataPreview()
    {
        if (SelectedElection is null)
        {
            return;
        }

        if (!CanEditDraft)
        {
            ValidationErrors = "Only DRAFT elections can be modified.";
            return;
        }

        var pendingElection = BuildElectionFromEditor(SelectedElection);
        var provisionalPda = $"pda_{pendingElection.Id:N}";

        var creationTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var metadataForHash = _metadataGenerator.BuildMetadata(
            pendingElection,
            provisionalPda,
            string.Empty,
            creationTimestamp);

        var canonicalJson = _metadataGenerator.Serialize(metadataForHash);
        var computedHash = _metadataHasher.ComputeSha256Hex(canonicalJson);

        var finalizedMetadata = _metadataGenerator.BuildMetadata(
            pendingElection,
            provisionalPda,
            computedHash,
            creationTimestamp);
        MetadataPreview = _metadataGenerator.Serialize(finalizedMetadata);
        MetadataHash = computedHash;
    }

    [RelayCommand]
    /// <summary>
    /// Executes Deploy Async.
    /// </summary>
    public async Task DeployAsync()
    {
        if (SelectedElection is null)
        {
            return;
        }

        if (!CanEditDraft)
        {
            ValidationErrors = "Only DRAFT elections can be modified.";
            return;
        }

        var pendingElection = BuildElectionFromEditor(SelectedElection);
        var validation = _validationService.ValidateForDeployment(pendingElection);
        if (!validation.IsValid)
        {
            ValidationErrors = string.Join(Environment.NewLine, validation.Errors);
            return;
        }

        IsBusy = true;
        try
        {
            var (updatedElection, result) = await _deploymentService.DeployAsync(pendingElection);
            ReplaceElection(updatedElection);
            SelectedElection = updatedElection;

            MetadataHash = result.MetadataHash;
            DeploymentSummary =
                $"Program ID: {result.ProgramId}{Environment.NewLine}" +
                $"Election PDA: {result.ElectionPda}{Environment.NewLine}" +
                $"Metadata URI: {result.MetadataUri}{Environment.NewLine}" +
                $"Transaction Signature: {result.TransactionSignature}{Environment.NewLine}" +
                $"Deployment Manifest: {updatedElection.DeploymentManifestPath}";

            ValidationErrors = string.Empty;
        }
        catch (Exception ex)
        {
            DeploymentSummary = "Deployment failed.";
            ValidationErrors = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    /// <summary>
    /// Executes Open Election.
    /// </summary>
    public void OpenElection()
    {
        if (SelectedElection is null)
        {
            ValidationErrors = "Select an election first.";
            return;
        }

        SelectedTabIndex = 1;
    }

    private void ReplaceElection(ElectionConfiguration election)
    {
        var index = Elections
            .Select((value, position) => new { value, position })
            .FirstOrDefault(x => x.value.Id == election.Id)?.position;

        if (index is int i)
        {
            Elections[i] = election;
        }
        else
        {
            Elections.Insert(0, election);
        }
    }

    private void LoadElectionToEditor(ElectionConfiguration election)
    {
        ElectionName = election.Name;
        ElectionDescription = election.Description;
        OpeningDate = election.OpeningDate;
        ClosingDate = election.ClosingDate;
        SelectedElectionType = election.ElectionType;

        SelectedNetwork = election.DeploymentSettings.Network;
        ProgramId = election.DeploymentSettings.ProgramId;
        MetadataEndpoint = election.DeploymentSettings.MetadataEndpoint;
        ExpectedVoters = election.DeploymentSettings.ExpectedVoters;
        PeakParticipationPercent = election.DeploymentSettings.PeakParticipationPercent;
        ElectionDurationHours = election.DeploymentSettings.ElectionDurationHours;
        ProgramVersion = election.DeploymentSettings.ProgramVersion;

        Candidates = new ObservableCollection<Candidate>(election.Candidates.OrderBy(c => c.DisplayOrder));

        MetadataHash = election.MetadataHash ?? string.Empty;
        DeploymentSummary = election.DeploymentSignature is null
            ? string.Empty
            : $"Program ID: {election.DeploymentSettings.ProgramId}{Environment.NewLine}" +
              $"Election PDA: {election.ElectionPda}{Environment.NewLine}" +
              $"Metadata URI: {election.MetadataUri}{Environment.NewLine}" +
                            $"Transaction Signature: {election.DeploymentSignature}{Environment.NewLine}" +
                            $"Deployment Manifest: {election.DeploymentManifestPath}";
    }

    private ElectionConfiguration BuildElectionFromEditor(ElectionConfiguration source)
    {
        return source with
        {
            Name = ElectionName.Trim(),
            Description = ElectionDescription.Trim(),
            OpeningDate = OpeningDate,
            ClosingDate = ClosingDate,
            ElectionType = SelectedElectionType,
            Candidates = Candidates.OrderBy(c => c.DisplayOrder).ToList(),
            DeploymentSettings = source.DeploymentSettings with
            {
                Network = SelectedNetwork,
                ProgramId = ProgramId.Trim(),
                MetadataEndpoint = MetadataEndpoint.Trim(),
                ExpectedVoters = ExpectedVoters,
                PeakParticipationPercent = PeakParticipationPercent,
                ElectionDurationHours = ElectionDurationHours,
                ProgramVersion = ProgramVersion.Trim()
            }
        };
    }
}
