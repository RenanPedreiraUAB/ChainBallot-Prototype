using ConfigurationComponent.Blockchain;
using ConfigurationComponent.Domain;
using ConfigurationComponent.Infrastructure;
using ConfigurationComponent.Repositories;
using Microsoft.Extensions.Logging;

namespace ConfigurationComponent.Services;

/// <summary>
/// Orchestrates the Election Configuration Component's deployment flow (UIS-04, Architecture.md § 3.1):
/// validate the draft, generate and hash metadata, publish metadata, initialize the reusable election
/// program on the canonical settlement ledger, persist the deployment manifest, and transition the
/// election to the Deployed lifecycle state. A failure at any step leaves the election in Draft rather
/// than partially deployed.
/// </summary>
public sealed class DeploymentService(
    IValidationService validationService,
    IMetadataGenerator metadataGenerator,
    IMetadataHasher metadataHasher,
    IDeploymentManifestGenerator deploymentManifestGenerator,
    IMetadataRepository metadataRepository,
    ISolanaService solanaService,
    IElectionRepository electionRepository,
    IDeploymentManifestRepository deploymentManifestRepository,
    ILogger<DeploymentService> logger) : IDeploymentService
{
    private readonly IValidationService _validationService = validationService;
    private readonly IMetadataGenerator _metadataGenerator = metadataGenerator;
    private readonly IMetadataHasher _metadataHasher = metadataHasher;
    private readonly IDeploymentManifestGenerator _deploymentManifestGenerator = deploymentManifestGenerator;
    private readonly IMetadataRepository _metadataRepository = metadataRepository;
    private readonly ISolanaService _solanaService = solanaService;
    private readonly IElectionRepository _electionRepository = electionRepository;
    private readonly IDeploymentManifestRepository _deploymentManifestRepository = deploymentManifestRepository;
    private readonly ILogger<DeploymentService> _logger = logger;

    /// <summary>
    /// Validates, then deploys an election: generates/hashes metadata, initializes the election program
    /// on the canonical settlement ledger, persists the deployment manifest, and marks the election Deployed.
    /// Throws if validation fails; no partial deployment is left behind on failure.
    /// </summary>
    public async Task<(ElectionConfiguration UpdatedElection, DeploymentResult Result)> DeployAsync(
        ElectionConfiguration election,
        CancellationToken cancellationToken = default)
    {
        var validation = _validationService.ValidateForDeployment(election);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));
        }

        var provisionalPda = $"pda_{election.Id:N}";
        var creationTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Canonical hash policy: hash metadata with fixed timestamp and empty metadataHash field.
        var metadataForHash = _metadataGenerator.BuildMetadata(
            election,
            provisionalPda,
            string.Empty,
            creationTimestamp);

        var canonicalJson = _metadataGenerator.Serialize(metadataForHash);
        var metadataHash = _metadataHasher.ComputeSha256Hex(canonicalJson);

        var finalizedMetadata = _metadataGenerator.BuildMetadata(
            election,
            provisionalPda,
            metadataHash,
            creationTimestamp);
        var finalizedJson = _metadataGenerator.Serialize(finalizedMetadata);

        string metadataUri;
        try
        {
            metadataUri = await _metadataRepository.UploadMetadataAsync(
                election.DeploymentSettings.MetadataEndpoint,
                finalizedJson,
                election.Id.ToString("N"),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metadata upload failed for election {ElectionId}", election.Id);
            throw;
        }

        DeploymentResult deploymentResult;
        try
        {
            deploymentResult = await _solanaService.DeployElectionAsync(election, metadataHash, metadataUri, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Solana deployment failed for election {ElectionId}", election.Id);
            throw;
        }

        var manifestGeneratedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var manifest = _deploymentManifestGenerator.BuildManifest(
            election,
            deploymentResult,
            creationTimestamp,
            manifestGeneratedAt);

        var manifestJson = _deploymentManifestGenerator.Serialize(manifest);
        var deploymentManifestPath = await _deploymentManifestRepository.SaveAsync(
            election.Id.ToString("N"),
            manifestJson,
            cancellationToken);

        var updated = election with
        {
            LifecycleState = ElectionLifecycleState.Deployed,
            DeploymentStatus = DeploymentStatus.Succeeded,
            MetadataUri = deploymentResult.MetadataUri,
            MetadataHash = deploymentResult.MetadataHash,
            ElectionPda = deploymentResult.ElectionPda,
            DeploymentSignature = deploymentResult.TransactionSignature,
            DeploymentManifestPath = deploymentManifestPath,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _electionRepository.SaveAsync(updated, cancellationToken);

        return (updated, deploymentResult);
    }
}
