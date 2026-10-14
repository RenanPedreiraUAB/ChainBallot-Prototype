namespace TransparencyAudit.Api.Services;

public sealed class InMemoryAuthoritativeEvidenceDigestSource : IAuthoritativeEvidenceDigestSource
{
    private readonly IReadOnlyDictionary<string, string> _metadataDigests;
    private readonly IReadOnlyDictionary<string, string> _auditDigests;

    public InMemoryAuthoritativeEvidenceDigestSource(
        IReadOnlyDictionary<string, string>? metadataDigests = null,
        IReadOnlyDictionary<string, string>? auditDigests = null)
    {
        _metadataDigests = metadataDigests ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _auditDigests = auditDigests ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public Task<string?> GetMetadataDigestAsync(string electionId, CancellationToken cancellationToken)
    {
        _metadataDigests.TryGetValue(electionId, out var digest);
        return Task.FromResult<string?>(digest);
    }

    public Task<string?> GetAuditDigestAsync(string electionId, CancellationToken cancellationToken)
    {
        _auditDigests.TryGetValue(electionId, out var digest);
        return Task.FromResult<string?>(digest);
    }
}
