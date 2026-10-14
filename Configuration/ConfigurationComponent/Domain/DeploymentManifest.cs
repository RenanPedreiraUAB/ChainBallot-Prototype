namespace ConfigurationComponent.Domain;

/// <summary>
/// Represents Deployment Manifest.
/// </summary>
public sealed class DeploymentManifest
{
    public required string SchemaVersion { get; init; }

    public required long GeneratedAt { get; init; }

    public required DeploymentManifestElection Election { get; init; }

    public required DeploymentManifestMetadata Metadata { get; init; }

    public required DeploymentManifestBlockchain Blockchain { get; init; }

    public required DeploymentManifestPolicy Policy { get; init; }

    public required DeploymentManifestHandoff Handoff { get; init; }
}

/// <summary>
/// Represents Deployment Manifest Election.
/// </summary>
public sealed class DeploymentManifestElection
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string ElectionType { get; init; }

    public required long OpeningTimestamp { get; init; }

    public required long ClosingTimestamp { get; init; }

    public required List<int> CandidateIds { get; init; }
}

/// <summary>
/// Represents Deployment Manifest Metadata.
/// </summary>
public sealed class DeploymentManifestMetadata
{
    public required string Uri { get; init; }

    public required string Hash { get; init; }

    public required string RepositoryEndpoint { get; init; }

    public required long CreationTimestamp { get; init; }
}

/// <summary>
/// Represents Deployment Manifest Blockchain.
/// </summary>
public sealed class DeploymentManifestBlockchain
{
    public required string Network { get; init; }

    public required string ProgramId { get; init; }

    public required string ProgramVersion { get; init; }

    public required string ElectionPda { get; init; }

    public required string InitializationSignature { get; init; }
}

/// <summary>
/// Represents Deployment Manifest Policy.
/// </summary>
public sealed class DeploymentManifestPolicy
{
    public required string BatchTreeType { get; init; }

    public required string ProofSystem { get; init; }

    public required string DecryptionMode { get; init; }
}

/// <summary>
/// Represents Deployment Manifest Handoff.
/// </summary>
public sealed class DeploymentManifestHandoff
{
    public required string ConsumerArtifactVersion { get; init; }

    public required string TransparencyArtifactVersion { get; init; }

    public required string WatcherArtifactVersion { get; init; }
}
