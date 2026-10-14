using TransparencyAudit.Api.Domain;

namespace TransparencyAudit.Api.Services;

/// <summary>
/// Computes deterministic consistency digests over projection content (Architecture.md § 3.8), so a
/// consumer (Result Portal, Watcher) can detect drift without re-deriving the whole projection state.
/// </summary>
public interface IConsistencyDigestCalculator
{
    /// <summary>Computes the whole-election projection's consistency digest.</summary>
    string ComputeElectionDigest(
        string electionId,
        string lifecycleState,
        string resultDigest,
        IReadOnlyDictionary<string, long> candidateTallies,
        IReadOnlyList<VidEvidenceEntry> vidEntries,
        string? metadataDigest,
        string? auditDigest);

    /// <summary>Computes a single VID entry's consistency digest (used for per-vote verification responses).</summary>
    string ComputeVidDigest(
        string electionId,
        string vid,
        string batchId,
        string ballotCommitment,
        string merkleRoot,
        int leafIndex);
}
