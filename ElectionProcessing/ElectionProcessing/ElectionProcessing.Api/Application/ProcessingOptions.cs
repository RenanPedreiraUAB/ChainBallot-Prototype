namespace ElectionProcessing.Api.Application;

public sealed class ProcessingOptions
{
    public const string SectionName = "Processing";

    public string QueueMode { get; set; } = QueueModes.InMemory;

    public KafkaOptions Kafka { get; set; } = new();

    public int MaxBallotsPerBatch { get; set; } = 32;

    public int MaxBatchWaitSeconds { get; set; } = 20;

    public int PollIntervalMilliseconds { get; set; } = 1000;

    public string SettlementNetwork { get; set; } = "solana-devnet";

    public string SettlementProgramId { get; set; } = "chainballot-program-v2";

    public string SettlementCommitment { get; set; } = "confirmed";

    public string SettlementMode { get; set; } = SettlementModes.Mock;

    public SolanaRpcOptions SolanaRpc { get; set; } = new();

    public string ProverMode { get; set; } = ProverModes.DeterministicStub;

    public ProjectionPublishingOptions ProjectionPublishing { get; set; } = new();

    public ExternalProverOptions ExternalProver { get; set; } = new();

    public LocalProcessProverOptions LocalProcessProver { get; set; } = new();

    public ProcessingAuthenticationOptions Authentication { get; set; } = new();
}

public static class QueueModes
{
    public const string InMemory = "InMemory";

    public const string Kafka = "Kafka";
}

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = string.Empty;

    public string Topic { get; set; } = "chainballot-ballots";

    public string ConsumerGroupId { get; set; } = "chainballot-outbound";

    public int PollBatchSize { get; set; } = 64;

    public int ConsumeTimeoutMilliseconds { get; set; } = 100;
}

public static class ProverModes
{
    public const string DeterministicStub = "DeterministicStub";

    public const string ExternalHttp = "ExternalHttp";

    public const string LocalProcess = "LocalProcess";
}

public static class SettlementModes
{
    public const string Mock = "Mock";

    public const string SolanaRpc = "SolanaRpc";
}

public sealed class SolanaRpcOptions
{
    public string RpcEndpoint { get; set; } = string.Empty;

    public string? RpcApiKey { get; set; }

    public string? AnchorSubmitMethod { get; set; } = "chainballot_anchorBatch";

    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class ExternalProverOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class ProjectionPublishingOptions
{
    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "http://localhost:5264";

    public int TimeoutSeconds { get; set; } = 10;

    public string LifecycleState { get; set; } = "Open";

    /// <summary>Header name sent with evidence publish requests; must match the target Transparency instance's configured credential header.</summary>
    public string AuthHeaderName { get; set; } = "X-Transparency-Auth";

    /// <summary>Credential value for the Transparency ingestion header. Required when the target Transparency instance enforces authentication on its ingest endpoint.</summary>
    public string? AuthToken { get; set; }

    /// <summary>Metadata digest endpoint template (use {electionId}), read before publishing so evidence carries the same authoritative digest Transparency independently verifies against. Null/empty disables the lookup.</summary>
    public string? MetadataDigestEndpointTemplate { get; set; }

    /// <summary>Audit digest endpoint template (use {electionId}); see <see cref="MetadataDigestEndpointTemplate"/>.</summary>
    public string? AuditDigestEndpointTemplate { get; set; }

    /// <summary>Timeout in seconds for authoritative digest HTTP lookups.</summary>
    public int DigestHttpTimeoutSeconds { get; set; } = 10;
}

public sealed class LocalProcessProverOptions
{
    public string ExecutablePath { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;
}
