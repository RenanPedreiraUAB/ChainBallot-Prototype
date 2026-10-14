using System.Text.Json;
using ConfigurationComponent.Domain;
using ConfigurationComponent.Utilities;

namespace ConfigurationComponent.Services;

/// <summary>
/// Generates the canonical election metadata document (Architecture.md § 3.1/§ 4.1): a stable JSON
/// representation of the election, schedule and candidate list that is hashed (see <see cref="IMetadataHasher"/>)
/// and published to the Election Metadata Repository. Any later mutation of this content is detectable
/// via hash mismatch.
/// </summary>
public sealed class MetadataGenerator : IMetadataGenerator
{
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Create();

    /// <summary>Builds the canonical <see cref="ElectionMetadata"/> document for an election, ready to be hashed and published.</summary>
    public ElectionMetadata BuildMetadata(
        ElectionConfiguration election,
        string electionPda,
        string metadataHash,
        long? creationTimestamp = null)
    {
        var timestamp = creationTimestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return new ElectionMetadata
        {
            Election = new ElectionMetadataElection
            {
                Id = election.Id.ToString("N"),
                Name = election.Name,
                Description = election.Description,
                Version = election.DeploymentSettings.ProgramVersion
            },
            Voting = new ElectionMetadataVoting
            {
                OpeningTimestamp = election.OpeningDate.ToUnixTimeSeconds(),
                ClosingTimestamp = election.ClosingDate.ToUnixTimeSeconds()
            },
            Blockchain = new ElectionMetadataBlockchain
            {
                Network = election.DeploymentSettings.Network.ToString().ToLowerInvariant(),
                ProgramId = election.DeploymentSettings.ProgramId,
                ElectionPda = electionPda
            },
            Candidates = election.Candidates
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new ElectionMetadataCandidate
                {
                    Id = c.Id,
                    Name = c.Name,
                    Party = c.Party,
                    Description = c.Description,
                    Image = c.Image
                })
                .ToList(),
            Verification = new ElectionMetadataVerification
            {
                MetadataHash = metadataHash,
                CreationTimestamp = timestamp
            }
        };
    }

    /// <summary>
    /// Executes Serialize.
    /// </summary>
    public string Serialize(ElectionMetadata metadata)
    {
        return JsonSerializer.Serialize(metadata, _jsonOptions);
    }
}
