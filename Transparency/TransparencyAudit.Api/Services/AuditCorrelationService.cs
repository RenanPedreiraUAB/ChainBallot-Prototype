namespace TransparencyAudit.Api.Services;

/// <summary>Default <see cref="IAuditCorrelationService"/> implementation; delegates to the shared correlation logic in <see cref="MetadataCorrelationService"/>.</summary>
public sealed class AuditCorrelationService : IAuditCorrelationService
{
    private readonly IAuthoritativeEvidenceDigestSource _authoritativeSource;

    public AuditCorrelationService(IAuthoritativeEvidenceDigestSource authoritativeSource)
    {
        _authoritativeSource = authoritativeSource;
    }

    public async Task<DigestCorrelationOutcome> CorrelateAsync(string electionId, string? providedDigest, CancellationToken cancellationToken)
    {
        var expected = await _authoritativeSource.GetAuditDigestAsync(electionId, cancellationToken).ConfigureAwait(false);
        return MetadataCorrelationService.Correlate("audit", expected, providedDigest);
    }
}
