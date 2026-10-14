namespace ElectionProcessing.Api.Processing;

/// <summary>
/// Reads the same authoritative metadata/audit digests Transparency independently verifies against,
/// so published projection evidence carries a digest Transparency's correlation check can match
/// (Architecture.md § 3.6/§ 3.8 consistency chain) instead of always being flagged stale.
/// </summary>
public interface IAuthoritativeDigestSource
{
    Task<string?> GetMetadataDigestAsync(string electionId, CancellationToken cancellationToken);

    Task<string?> GetAuditDigestAsync(string electionId, CancellationToken cancellationToken);
}
