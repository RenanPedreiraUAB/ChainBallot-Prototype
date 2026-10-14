namespace WatcherAudit.Api.Domain;

public sealed record AuthoritativeDigestPair(
    string? MetadataDigest,
    string? AuditDigest);
