namespace ConfigurationComponent.Domain;

/// <summary>
/// Represents Deployment Settings.
/// </summary>
public sealed record DeploymentSettings(
    SolanaNetwork Network,
    string ProgramId,
    string MetadataEndpoint,
    int ExpectedVoters,
    int PeakParticipationPercent,
    int ElectionDurationHours,
    string ProgramVersion,
    string BatchTreeType,
    string ProofSystem,
    string DecryptionMode);

/// <summary>
/// Represents Election Configuration.
/// </summary>
public sealed record ElectionConfiguration(
    Guid Id,
    string Name,
    string Description,
    DateTimeOffset OpeningDate,
    DateTimeOffset ClosingDate,
    ElectionType ElectionType,
    IReadOnlyList<Candidate> Candidates,
    DeploymentSettings DeploymentSettings,
    ElectionLifecycleState LifecycleState,
    DeploymentStatus DeploymentStatus,
    string? MetadataUri,
    string? MetadataHash,
    string? ElectionPda,
    string? DeploymentSignature,
    string? DeploymentManifestPath,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public bool IsDraft => LifecycleState == ElectionLifecycleState.Draft;
}
