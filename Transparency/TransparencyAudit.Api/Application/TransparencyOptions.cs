namespace TransparencyAudit.Api.Application;

/// <summary>
/// Runtime options for Transparency/Audit API projection persistence.
/// </summary>
public sealed class TransparencyOptions
{
    public const string SectionName = "Transparency";

    /// <summary>
    /// Full path to projection store file. If empty, a default LocalApplicationData path is used.
    /// </summary>
    public string? ProjectionStorePath { get; set; }

    /// <summary>
    /// Authoritative digest source mode. Supported values: InMemory, Http.
    /// </summary>
    public string AuthoritativeDigestSourceMode { get; set; } = "Http";

    /// <summary>
    /// Metadata digest endpoint template used when mode is Http. Use {electionId} token.
    /// </summary>
    public string? MetadataDigestEndpointTemplate { get; set; } = "http://localhost:8088/digests/metadata/{electionId}.json";

    /// <summary>
    /// Audit digest endpoint template used when mode is Http. Use {electionId} token.
    /// </summary>
    public string? AuditDigestEndpointTemplate { get; set; } = "http://localhost:8088/digests/audit/{electionId}.json";

    /// <summary>
    /// Timeout in seconds for authoritative HTTP calls.
    /// </summary>
    public int AuthoritativeHttpTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Optional local authoritative metadata digests keyed by election id.
    /// </summary>
    public Dictionary<string, string> AuthoritativeMetadataDigests { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional local authoritative audit digests keyed by election id.
    /// </summary>
    public Dictionary<string, string> AuthoritativeAuditDigests { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Enables periodic automated reindex attempts for configured election ids.
    /// </summary>
    public bool AutoReindexEnabled { get; set; }

    /// <summary>
    /// Auto reindex interval in seconds.
    /// </summary>
    public int AutoReindexIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Election ids included in periodic reindex orchestration.
    /// </summary>
    public string[] AutoReindexElectionIds { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Canonical Solana read mode. Supported values: ProjectionOnly, Mock, Http.
    /// </summary>
    public string SolanaReadMode { get; set; } = "ProjectionOnly";

    /// <summary>
    /// Election state endpoint template for canonical Solana reads when mode is Http. Use {electionId} token.
    /// </summary>
    public string? SolanaElectionStateEndpointTemplate { get; set; }

    /// <summary>
    /// Timeout in seconds for canonical Solana HTTP reads.
    /// </summary>
    public int SolanaHttpTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Optional mock finalized states keyed by election id when mode is Mock.
    /// </summary>
    public Dictionary<string, bool> MockSolanaFinalizedStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public TransparencyAuthenticationOptions Authentication { get; set; } = new();
}

/// <summary>
/// Header-token credentials protecting Transparency's privileged routes (Architecture.md § 3.8):
/// evidence ingestion/mark-stale/reindex/resync require <see cref="TransparencyRoles.Ingestion"/>;
/// reconciliation disputes/override/resolve require <see cref="TransparencyRoles.Reconciliation"/>.
/// Prototype-grade shared-secret auth, consistent with the <c>X-Operator-Key</c> pattern already used
/// by the Watcher and Threshold Decryption components.
/// </summary>
public sealed class TransparencyAuthenticationOptions
{
    public string HeaderName { get; set; } = "X-Transparency-Auth";

    public List<TransparencyApiCredential> Credentials { get; set; } = [];
}

public sealed class TransparencyApiCredential
{
    public string Token { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];
}

/// <summary>Role names granted by a matched <see cref="TransparencyApiCredential"/>.</summary>
public static class TransparencyRoles
{
    public const string Ingestion = "transparency.ingestion";

    public const string Reconciliation = "transparency.reconciliation";
}
