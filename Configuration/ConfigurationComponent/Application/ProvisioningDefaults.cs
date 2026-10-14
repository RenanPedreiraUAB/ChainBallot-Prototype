using ConfigurationComponent.Domain;

namespace ConfigurationComponent.Application;

/// <summary>
/// Represents Provisioning Defaults.
/// </summary>
public sealed class ProvisioningDefaults
{
    public string NewElectionName { get; set; } = "New Election";

    public SolanaNetwork Network { get; set; } = SolanaNetwork.Devnet;

    public string ProgramId { get; set; } = "DefaultProgramId";

    public string MetadataEndpoint { get; set; } = "https://metadata.example/api/";

    public int ExpectedVoters { get; set; } = 1000;

    public int PeakParticipationPercent { get; set; } = 25;

    public int ElectionDurationHours { get; set; } = 24;

    public string ProgramVersion { get; set; } = "1.0.0";

    public string BatchTreeType { get; set; } = "merkle-commitment-tree";

    public string ProofSystem { get; set; } = "zk-rollup";

    public string DecryptionMode { get; set; } = "threshold-after-close";

    public int OpeningOffsetDays { get; set; } = 1;

    public int ClosingOffsetDays { get; set; } = 2;
}
