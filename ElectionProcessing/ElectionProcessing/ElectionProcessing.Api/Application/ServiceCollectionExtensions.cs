using ElectionProcessing.Api.Processing;
using ElectionProcessing.Api.Security;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace ElectionProcessing.Api.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddElectionProcessingCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ProcessingOptions>()
            .Bind(configuration.GetSection(ProcessingOptions.SectionName))
            .Validate(x =>
                string.Equals(x.QueueMode, QueueModes.InMemory, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.QueueMode, QueueModes.Kafka, StringComparison.OrdinalIgnoreCase),
                "QueueMode must be either InMemory or Kafka.")
            .Validate(x =>
                !string.Equals(x.QueueMode, QueueModes.Kafka, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(x.Kafka.BootstrapServers),
                "Processing:Kafka:BootstrapServers is required when QueueMode is Kafka.")
            .Validate(x =>
                !string.Equals(x.QueueMode, QueueModes.Kafka, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(x.Kafka.Topic),
                "Processing:Kafka:Topic is required when QueueMode is Kafka.")
            .Validate(x =>
                !string.Equals(x.QueueMode, QueueModes.Kafka, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(x.Kafka.ConsumerGroupId),
                "Processing:Kafka:ConsumerGroupId is required when QueueMode is Kafka.")
            .Validate(x => x.Kafka.PollBatchSize > 0, "Processing:Kafka:PollBatchSize must be greater than zero.")
            .Validate(x => x.Kafka.ConsumeTimeoutMilliseconds > 0, "Processing:Kafka:ConsumeTimeoutMilliseconds must be greater than zero.")
            .Validate(x => x.MaxBallotsPerBatch > 0, "MaxBallotsPerBatch must be greater than zero.")
            .Validate(x => x.MaxBatchWaitSeconds > 0, "MaxBatchWaitSeconds must be greater than zero.")
            .Validate(x => x.PollIntervalMilliseconds > 0, "PollIntervalMilliseconds must be greater than zero.")
            .Validate(x => x.ProjectionPublishing.TimeoutSeconds > 0, "Processing:ProjectionPublishing:TimeoutSeconds must be greater than zero.")
            .Validate(x =>
                !x.ProjectionPublishing.Enabled
                || Uri.TryCreate(x.ProjectionPublishing.BaseUrl, UriKind.Absolute, out _),
                "Processing:ProjectionPublishing:BaseUrl must be an absolute URI when projection publishing is enabled.")
            .Validate(x =>
                !x.ProjectionPublishing.Enabled
                || !string.IsNullOrWhiteSpace(x.ProjectionPublishing.LifecycleState),
                "Processing:ProjectionPublishing:LifecycleState is required when projection publishing is enabled.")
            .Validate(x =>
                string.Equals(x.ProverMode, ProverModes.DeterministicStub, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.ProverMode, ProverModes.ExternalHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.ProverMode, ProverModes.LocalProcess, StringComparison.OrdinalIgnoreCase),
                "ProverMode must be either DeterministicStub, ExternalHttp, or LocalProcess.")
            .Validate(x =>
                !string.Equals(x.ProverMode, ProverModes.ExternalHttp, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(x.ExternalProver.Endpoint),
                "Processing:ExternalProver:Endpoint is required when ProverMode is ExternalHttp.")
            .Validate(x => x.ExternalProver.TimeoutSeconds > 0, "Processing:ExternalProver:TimeoutSeconds must be greater than zero.")
            .Validate(x =>
                !string.Equals(x.ProverMode, ProverModes.LocalProcess, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(x.LocalProcessProver.ExecutablePath),
                "Processing:LocalProcessProver:ExecutablePath is required when ProverMode is LocalProcess.")
            .Validate(x => x.LocalProcessProver.TimeoutSeconds > 0, "Processing:LocalProcessProver:TimeoutSeconds must be greater than zero.")
            .Validate(x =>
                string.Equals(x.SettlementMode, SettlementModes.Mock, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.SettlementMode, SettlementModes.SolanaRpc, StringComparison.OrdinalIgnoreCase),
                "SettlementMode must be either Mock or SolanaRpc.")
            .Validate(x =>
                !string.Equals(x.SettlementMode, SettlementModes.SolanaRpc, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(x.SolanaRpc.RpcEndpoint),
                "Processing:SolanaRpc:RpcEndpoint is required when SettlementMode is SolanaRpc.")
            .Validate(x =>
                !string.Equals(x.SettlementMode, SettlementModes.SolanaRpc, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(x.SettlementNetwork),
                "Processing:SettlementNetwork is required when SettlementMode is SolanaRpc.")
            .Validate(x =>
                !string.Equals(x.SettlementMode, SettlementModes.SolanaRpc, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(x.SettlementProgramId),
                "Processing:SettlementProgramId is required when SettlementMode is SolanaRpc.")
            .Validate(x => x.SolanaRpc.TimeoutSeconds > 0, "Processing:SolanaRpc:TimeoutSeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<ISystemClock, UtcSystemClock>();
        services.AddSingleton<InMemoryBallotQueueAdapter>();
        services.AddSingleton<KafkaBallotQueueAdapter>();
        services.AddSingleton<IBallotQueueIngress>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ProcessingOptions>>().Value;
            return string.Equals(options.QueueMode, QueueModes.Kafka, StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<KafkaBallotQueueAdapter>()
                : sp.GetRequiredService<InMemoryBallotQueueAdapter>();
        });
        services.AddSingleton<IBallotQueueConsumer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ProcessingOptions>>().Value;
            return string.Equals(options.QueueMode, QueueModes.Kafka, StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<KafkaBallotQueueAdapter>()
                : sp.GetRequiredService<InMemoryBallotQueueAdapter>();
        });

        services.AddSingleton<IBatchValidationService, BatchValidationService>();
        services.AddSingleton<ITreeStrategy, BinarySha256MerkleTreeStrategy>();

        services.AddHttpClient<HttpProjectionEvidencePublisher>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ProcessingOptions>>().Value;
            client.BaseAddress = new Uri(options.ProjectionPublishing.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(options.ProjectionPublishing.TimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(options.ProjectionPublishing.AuthToken))
            {
                client.DefaultRequestHeaders.Add(options.ProjectionPublishing.AuthHeaderName, options.ProjectionPublishing.AuthToken);
            }
        });
        services.AddSingleton<NoOpProjectionEvidencePublisher>();
        services.AddSingleton<IProjectionEvidencePublisher>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ProcessingOptions>>().Value;
            return options.ProjectionPublishing.Enabled
                ? sp.GetRequiredService<HttpProjectionEvidencePublisher>()
                : sp.GetRequiredService<NoOpProjectionEvidencePublisher>();
        });

        services.AddHttpClient("ProcessingAuthoritativeDigests");
        services.AddSingleton<IAuthoritativeDigestSource>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ProcessingOptions>>().Value;
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient("ProcessingAuthoritativeDigests");
            httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.ProjectionPublishing.DigestHttpTimeoutSeconds));

            return new HttpAuthoritativeDigestSource(
                httpClient,
                options.ProjectionPublishing.MetadataDigestEndpointTemplate,
                options.ProjectionPublishing.AuditDigestEndpointTemplate);
        });

        services.AddSingleton<DeterministicStubZkProofStrategy>();
        services.AddSingleton<LocalProcessZkProofStrategy>();
        services.AddHttpClient<ExternalHttpZkProofStrategy>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ProcessingOptions>>().Value;
            var endpoint = options.ExternalProver.Endpoint;
            if (!string.IsNullOrWhiteSpace(endpoint) && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            {
                client.BaseAddress = new Uri(uri.GetLeftPart(UriPartial.Authority), UriKind.Absolute);
            }

            client.Timeout = TimeSpan.FromSeconds(options.ExternalProver.TimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(options.ExternalProver.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ExternalProver.ApiKey);
            }
        });

        services.AddSingleton<IZkProofStrategy>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ProcessingOptions>>().Value;
            if (string.Equals(options.ProverMode, ProverModes.ExternalHttp, StringComparison.OrdinalIgnoreCase))
            {
                return sp.GetRequiredService<ExternalHttpZkProofStrategy>();
            }

            if (string.Equals(options.ProverMode, ProverModes.LocalProcess, StringComparison.OrdinalIgnoreCase))
            {
                return sp.GetRequiredService<LocalProcessZkProofStrategy>();
            }

            return sp.GetRequiredService<DeterministicStubZkProofStrategy>();
        });

        services.AddSingleton<DeterministicStubProofArtifactVerifier>();
        services.AddSingleton<BasicProofArtifactVerifier>();
        services.AddSingleton<IProofArtifactVerifier>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ProcessingOptions>>().Value;
            return string.Equals(options.ProverMode, ProverModes.DeterministicStub, StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<DeterministicStubProofArtifactVerifier>()
                : sp.GetRequiredService<BasicProofArtifactVerifier>();
        });

            services.AddHttpClient<SolanaRpcSettlementAnchor>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<ProcessingOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(options.SolanaRpc.TimeoutSeconds);

                if (!string.IsNullOrWhiteSpace(options.SolanaRpc.RpcApiKey))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.SolanaRpc.RpcApiKey);
                }
            });
            services.AddSingleton<MockSettlementAnchor>();
            services.AddSingleton<ISettlementAnchor>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<ProcessingOptions>>().Value;
                return string.Equals(options.SettlementMode, SettlementModes.SolanaRpc, StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<SolanaRpcSettlementAnchor>()
                : sp.GetRequiredService<MockSettlementAnchor>();
            });

            services.AddHttpClient(nameof(ProverReadinessService));
            services.AddSingleton<IProverReadinessService, ProverReadinessService>();
        services.AddSingleton<IProcessingCoordinator, ProcessingCoordinator>();

        services.AddHostedService<ElectionProcessingWorker>();

        services
            .AddAuthentication(ProcessingHeaderAuthenticationHandler.SchemeName)
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ProcessingHeaderAuthenticationHandler>(ProcessingHeaderAuthenticationHandler.SchemeName, _ => { });
        services.AddAuthorization();

        return services;
    }
}
