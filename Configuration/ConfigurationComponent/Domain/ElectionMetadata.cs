namespace ConfigurationComponent.Domain;

/// <summary>
/// Represents Election Metadata.
/// </summary>
public sealed class ElectionMetadata
{
    public required ElectionMetadataElection Election { get; init; }

    public required ElectionMetadataVoting Voting { get; init; }

    public required ElectionMetadataBlockchain Blockchain { get; init; }

    public required List<ElectionMetadataCandidate> Candidates { get; init; }

    public required ElectionMetadataVerification Verification { get; init; }
}

/// <summary>
/// Represents Election Metadata Election.
/// </summary>
public sealed class ElectionMetadataElection
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Version { get; init; }
}

/// <summary>
/// Represents Election Metadata Voting.
/// </summary>
public sealed class ElectionMetadataVoting
{
    public required long OpeningTimestamp { get; init; }

    public required long ClosingTimestamp { get; init; }
}

/// <summary>
/// Represents Election Metadata Blockchain.
/// </summary>
public sealed class ElectionMetadataBlockchain
{
    public required string Network { get; init; }

    public required string ProgramId { get; init; }

    public required string ElectionPda { get; init; }
}

/// <summary>
/// Represents Election Metadata Candidate.
/// </summary>
public sealed class ElectionMetadataCandidate
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string Party { get; init; }

    public required string Description { get; init; }

    public required string Image { get; init; }
}

/// <summary>
/// Represents Election Metadata Verification.
/// </summary>
public sealed class ElectionMetadataVerification
{
    public required string MetadataHash { get; init; }

    public required long CreationTimestamp { get; init; }
}
