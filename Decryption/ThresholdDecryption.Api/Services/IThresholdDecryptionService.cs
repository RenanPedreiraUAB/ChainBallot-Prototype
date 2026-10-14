using ThresholdDecryption.Api.Domain;

namespace ThresholdDecryption.Api.Services;

/// <summary>
/// Governance-ceremony orchestration for the Threshold Decryption Component (Architecture.md § 3.11).
/// Finalization is only reachable once independent governance approvals and independent trustee key
/// shares both satisfy their configured thresholds — no single call can unilaterally finalize a result.
/// Note: the final candidate tallies are supplied by the caller at the finalize step in this prototype
/// (see Prototype.md § 4.11); this service governs the <c>release</c> of the result, not the underlying
/// cryptographic decryption math.
/// </summary>
public interface IThresholdDecryptionService
{
    /// <summary>Proposes a new decryption ceremony for an election with the required approval/share thresholds.</summary>
    Task<ThresholdCeremony> ProposeCeremonyAsync(string electionId, int? requiredShares, int? requiredApprovals, string actor, CancellationToken cancellationToken);

    /// <summary>Records one governance approval toward the ceremony's required-approvals threshold.</summary>
    Task<ThresholdCeremony?> ApproveCeremonyAsync(string electionId, string approverId, string note, CancellationToken cancellationToken);

    /// <summary>Records one trustee key-share digest toward the ceremony's required-shares threshold.</summary>
    Task<ThresholdCeremony?> SubmitShareAsync(string electionId, string trusteeId, string shareDigest, CancellationToken cancellationToken);

    /// <summary>
    /// Finalizes the ceremony once both thresholds are satisfied, computing a deterministic result
    /// digest over the supplied decrypted tallies.
    /// </summary>
    Task<ThresholdCeremony?> FinalizeDecryptionAsync(string electionId, IReadOnlyDictionary<string, long> decryptedTallies, string actor, CancellationToken cancellationToken);

    /// <summary>Publishes the finalized tally, returning a publication reference for downstream ingestion into Transparency.</summary>
    Task<ThresholdCeremony?> PublishFinalTallyAsync(string electionId, string actor, CancellationToken cancellationToken);
}
