using System.Text.Json;
using ConfigurationComponent.Domain;
using ConfigurationComponent.Utilities;

namespace ConfigurationComponent.Services;

/// <summary>
/// Builds the deployment manifest (Architecture.md § 4.1, "stable handoff artifact"): a schema-versioned
/// snapshot binding the election definition, metadata reference/hash, canonical-ledger program reference
/// and cryptographic policy together, so downstream components (Processing, Transparency, Watcher) can
/// treat it as the single source of truth for "what was configured".
/// </summary>
public sealed class DeploymentManifestGenerator : IDeploymentManifestGenerator
{
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Create();

    /// <summary>Builds the deployment manifest for a successfully deployed election.</summary>
    public DeploymentManifest BuildManifest(
        ElectionConfiguration election,
        DeploymentResult deploymentResult,
        long metadataCreationTimestamp,
        long generatedAtTimestamp)
    {
        return new DeploymentManifest
        {
            SchemaVersion = "2.0.0",
            GeneratedAt = generatedAtTimestamp,
            Election = new DeploymentManifestElection
            {
                Id = election.Id.ToString("N"),
                Name = election.Name,
                Description = election.Description,
                ElectionType = election.ElectionType.ToString(),
                OpeningTimestamp = election.OpeningDate.ToUnixTimeSeconds(),
                ClosingTimestamp = election.ClosingDate.ToUnixTimeSeconds(),
                CandidateIds = election.Candidates
                    .OrderBy(c => c.DisplayOrder)
                    .Select(c => c.Id)
                    .ToList()
            },
            Metadata = new DeploymentManifestMetadata
            {
                Uri = deploymentResult.MetadataUri,
                Hash = deploymentResult.MetadataHash,
                RepositoryEndpoint = election.DeploymentSettings.MetadataEndpoint,
                CreationTimestamp = metadataCreationTimestamp
            },
            Blockchain = new DeploymentManifestBlockchain
            {
                Network = election.DeploymentSettings.Network.ToString().ToLowerInvariant(),
                ProgramId = deploymentResult.ProgramId,
                ProgramVersion = election.DeploymentSettings.ProgramVersion,
                ElectionPda = deploymentResult.ElectionPda,
                InitializationSignature = deploymentResult.TransactionSignature
            },
            Policy = new DeploymentManifestPolicy
            {
                BatchTreeType = election.DeploymentSettings.BatchTreeType,
                ProofSystem = election.DeploymentSettings.ProofSystem,
                DecryptionMode = election.DeploymentSettings.DecryptionMode
            },
            Handoff = new DeploymentManifestHandoff
            {
                ConsumerArtifactVersion = "v2",
                TransparencyArtifactVersion = "v2",
                WatcherArtifactVersion = "v2"
            }
        };
    }

    /// <summary>
    /// Executes Serialize.
    /// </summary>
    public string Serialize(DeploymentManifest manifest)
    {
        return JsonSerializer.Serialize(manifest, _jsonOptions);
    }
}
