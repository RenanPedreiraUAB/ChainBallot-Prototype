using System.Text.Json;
using ConfigurationComponent.Services;
using ConfigurationComponent.Tests.Helpers;

namespace ConfigurationComponent.Tests;

/// <summary>
/// Represents Metadata Services Tests.
/// </summary>
public sealed class MetadataServicesTests
{
    [Fact]
    /// <summary>
    /// Executes Compute Sha256 Hex_Returns Known Hash.
    /// </summary>
    public void ComputeSha256Hex_ReturnsKnownHash()
    {
        var hasher = new MetadataHasher();

        var hash = hasher.ComputeSha256Hex("abc");

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    [Fact]
    /// <summary>
    /// Executes Build Metadata_Maps Election Fields And Candidates.
    /// </summary>
    public void BuildMetadata_MapsElectionFieldsAndCandidates()
    {
        var generator = new MetadataGenerator();
        var election = TestDataFactory.CreateValidDraftElection();

        var metadata = generator.BuildMetadata(election, electionPda: "pda_test", metadataHash: "hash_test");

        Assert.Equal(election.Id.ToString("N"), metadata.Election.Id);
        Assert.Equal(election.Name, metadata.Election.Name);
        Assert.Equal("devnet", metadata.Blockchain.Network);
        Assert.Equal("pda_test", metadata.Blockchain.ElectionPda);
        Assert.Equal("hash_test", metadata.Verification.MetadataHash);
        Assert.Equal(2, metadata.Candidates.Count);
        Assert.Equal(1, metadata.Candidates[0].Id);
        Assert.Equal(2, metadata.Candidates[1].Id);
    }

    [Fact]
    /// <summary>
    /// Executes Serialize_Produces Json With Expected Sections.
    /// </summary>
    public void Serialize_ProducesJsonWithExpectedSections()
    {
        var generator = new MetadataGenerator();
        var election = TestDataFactory.CreateValidDraftElection();
        var metadata = generator.BuildMetadata(election, electionPda: "pda_test", metadataHash: "hash_test");

        var json = generator.Serialize(metadata);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("election", out _));
        Assert.True(root.TryGetProperty("voting", out _));
        Assert.True(root.TryGetProperty("blockchain", out _));
        Assert.True(root.TryGetProperty("candidates", out _));
        Assert.True(root.TryGetProperty("verification", out _));

        var verification = root.GetProperty("verification");
        Assert.Equal("hash_test", verification.GetProperty("metadataHash").GetString());
    }
}
