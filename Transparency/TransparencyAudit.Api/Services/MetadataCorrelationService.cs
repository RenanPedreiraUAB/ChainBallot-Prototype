namespace TransparencyAudit.Api.Services;

/// <summary>Default <see cref="IMetadataCorrelationService"/> implementation; compares a provided digest against the authoritative metadata digest source.</summary>
public sealed class MetadataCorrelationService : IMetadataCorrelationService
{
    private readonly IAuthoritativeEvidenceDigestSource _authoritativeSource;

    public MetadataCorrelationService(IAuthoritativeEvidenceDigestSource authoritativeSource)
    {
        _authoritativeSource = authoritativeSource;
    }

    public async Task<DigestCorrelationOutcome> CorrelateAsync(string electionId, string? providedDigest, CancellationToken cancellationToken)
    {
        var expected = await _authoritativeSource.GetMetadataDigestAsync(electionId, cancellationToken).ConfigureAwait(false);
        return Correlate("metadata", expected, providedDigest);
    }

    internal static DigestCorrelationOutcome Correlate(string domain, string? expected, string? provided)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return new DigestCorrelationOutcome(domain, true, null, provided, provided, null);
        }

        if (string.IsNullOrWhiteSpace(provided))
        {
            return new DigestCorrelationOutcome(
                domain,
                false,
                expected,
                provided,
                expected,
                $"{domain} digest missing while authoritative digest exists.");
        }

        if (!string.Equals(expected, provided, StringComparison.OrdinalIgnoreCase))
        {
            return new DigestCorrelationOutcome(
                domain,
                false,
                expected,
                provided,
                expected,
                $"{domain} digest mismatch against authoritative source.");
        }

        return new DigestCorrelationOutcome(domain, true, expected, provided, expected, null);
    }
}
