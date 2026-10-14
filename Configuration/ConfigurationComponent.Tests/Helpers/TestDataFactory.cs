using ConfigurationComponent.Domain;

namespace ConfigurationComponent.Tests.Helpers;

internal static class TestDataFactory
{
    /// <summary>
    /// Executes Create Valid Draft Election.
    /// </summary>
    public static ElectionConfiguration CreateValidDraftElection()
    {
        var now = DateTimeOffset.UtcNow;

        return new ElectionConfiguration(
            Id: Guid.NewGuid(),
            Name: "Municipal Election",
            Description: "Election for city council",
            OpeningDate: now.AddDays(2),
            ClosingDate: now.AddDays(3),
            ElectionType: ElectionType.SingleChoice,
            Candidates:
            [
                new Candidate(1, "Alice", "Party A", "Candidate A", "https://img/a.png", 1),
                new Candidate(2, "Bob", "Party B", "Candidate B", "https://img/b.png", 2)
            ],
            DeploymentSettings: new DeploymentSettings(
                Network: SolanaNetwork.Devnet,
                ProgramId: "Program123",
                MetadataEndpoint: "https://metadata.example/api/",
                ExpectedVoters: 1000,
                PeakParticipationPercent: 40,
                ElectionDurationHours: 24,
                ProgramVersion: "1.0.0",
                BatchTreeType: "merkle-commitment-tree",
                ProofSystem: "zk-rollup",
                DecryptionMode: "threshold-after-close"),
            LifecycleState: ElectionLifecycleState.Draft,
            DeploymentStatus: DeploymentStatus.NotStarted,
            MetadataUri: null,
            MetadataHash: null,
            ElectionPda: null,
            DeploymentSignature: null,
            DeploymentManifestPath: null,
            CreatedAt: now,
            UpdatedAt: now);
    }
}
