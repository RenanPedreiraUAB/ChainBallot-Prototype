namespace WatcherAudit.Api.Application;

public sealed class WatcherOptions
{
    public const string SectionName = "Watcher";

    public string TransparencyBaseUrl { get; set; } = "http://localhost:5284";

    public int TransparencyTimeoutSeconds { get; set; } = 10;

    public string DigestSourceMode { get; set; } = "Http";

    public int DigestHttpTimeoutSeconds { get; set; } = 5;

    public string MetadataDigestEndpointTemplate { get; set; } = "http://localhost:8088/digests/metadata/{electionId}.json";

    public string AuditDigestEndpointTemplate { get; set; } = "http://localhost:8088/digests/audit/{electionId}.json";

    public Dictionary<string, string> MetadataDigests { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> AuditDigests { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string ReportStorePath { get; set; } = string.Empty;

    public string ReportSigningKey { get; set; } = "watcher-dev-key";

    public string OperatorAccessHeaderName { get; set; } = "X-Operator-Key";

    public string OperatorAccessKey { get; set; } = "watcher-operator-key";
}
