using WatcherAudit.Api.Domain;

namespace WatcherAudit.Api.Services;

/// <summary>
/// Independent source of authoritative metadata/audit digests, kept separate from the Transparency
/// projection itself so the Watcher Component can detect divergence rather than trusting the same
/// data twice (Architecture.md § 3.10). <c>InMemory</c> and <c>Http</c> adapter modes exist.
/// </summary>
public interface IAuthoritativeDigestSource
{
    /// <summary>Retrieves the authoritative metadata/audit digest pair for an election.</summary>
    Task<AuthoritativeDigestPair> GetDigestsAsync(string electionId, CancellationToken cancellationToken);
}
