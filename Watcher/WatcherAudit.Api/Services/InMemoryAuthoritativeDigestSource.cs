using WatcherAudit.Api.Domain;

namespace WatcherAudit.Api.Services;

public sealed class InMemoryAuthoritativeDigestSource : IAuthoritativeDigestSource
{
    private readonly IReadOnlyDictionary<string, string> _metadataDigests;
    private readonly IReadOnlyDictionary<string, string> _auditDigests;

    public InMemoryAuthoritativeDigestSource(
        IReadOnlyDictionary<string, string> metadataDigests,
        IReadOnlyDictionary<string, string> auditDigests)
    {
        _metadataDigests = metadataDigests;
        _auditDigests = auditDigests;
    }

    public Task<AuthoritativeDigestPair> GetDigestsAsync(string electionId, CancellationToken cancellationToken)
    {
        _metadataDigests.TryGetValue(electionId, out var metadataDigest);
        _auditDigests.TryGetValue(electionId, out var auditDigest);
        return Task.FromResult(new AuthoritativeDigestPair(metadataDigest, auditDigest));
    }
}
