using System.Text.Json;
using ConfigurationComponent.Services;
using ConfigurationComponent.Tests.Helpers;

namespace ConfigurationComponent.Tests;

/// <summary>
/// Represents Deployment Manifest Generator Tests.
/// </summary>
public sealed class DeploymentManifestGeneratorTests
{
    [Fact]
    /// <summary>
    /// Executes Build Manifest_Produces Expected Schema And Fields.
    /// </summary>
    public void BuildManifest_ProducesExpectedSchemaAndFields()
    {
        var election = TestDataFactory.CreateValidDraftElection();
        var deploymentResult = new ConfigurationComponent.Domain.DeploymentResult(
            ProgramId: election.DeploymentSettings.ProgramId,
            ElectionPda: $"pda_{election.Id:N}",
            MetadataUri: "https://metadata.local/election.json",
            TransactionSignature: "tx_signature",
            MetadataHash: "abc123hash");

        var generator = new DeploymentManifestGenerator();

        var manifest = generator.BuildManifest(
            election,
            deploymentResult,
            metadataCreationTimestamp: 1700000000,
            generatedAtTimestamp: 1700000005);

        Assert.Equal("2.0.0", manifest.SchemaVersion);
        Assert.Equal(election.Id.ToString("N"), manifest.Election.Id);
        Assert.Equal("https://metadata.local/election.json", manifest.Metadata.Uri);
        Assert.Equal("abc123hash", manifest.Metadata.Hash);
        Assert.Equal("zk-rollup", manifest.Policy.ProofSystem);
        Assert.Equal("threshold-after-close", manifest.Policy.DecryptionMode);
    }

    [Fact]
    /// <summary>
    /// Executes Serialize_Emits Expected Manifest Sections.
    /// </summary>
    public void Serialize_EmitsExpectedManifestSections()
    {
        var election = TestDataFactory.CreateValidDraftElection();
        var deploymentResult = new ConfigurationComponent.Domain.DeploymentResult(
            ProgramId: election.DeploymentSettings.ProgramId,
            ElectionPda: $"pda_{election.Id:N}",
            MetadataUri: "https://metadata.local/election.json",
            TransactionSignature: "tx_signature",
            MetadataHash: "abc123hash");

        var generator = new DeploymentManifestGenerator();
        var manifest = generator.BuildManifest(
            election,
            deploymentResult,
            metadataCreationTimestamp: 1700000000,
            generatedAtTimestamp: 1700000005);

        var json = generator.Serialize(manifest);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("schemaVersion", out _));
        Assert.True(root.TryGetProperty("election", out _));
        Assert.True(root.TryGetProperty("metadata", out _));
        Assert.True(root.TryGetProperty("blockchain", out _));
        Assert.True(root.TryGetProperty("policy", out _));
        Assert.True(root.TryGetProperty("handoff", out _));
    }
}
