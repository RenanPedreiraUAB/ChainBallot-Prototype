namespace TransparencyAudit.Api.Services;

public interface IAuthoritativeEvidenceDigestSource
{
    Task<string?> GetMetadataDigestAsync(string electionId, CancellationToken cancellationToken);

    Task<string?> GetAuditDigestAsync(string electionId, CancellationToken cancellationToken);
}
