using Microsoft.Extensions.Options;
using TransparencyAudit.Api.Security;
using TransparencyAudit.Api.Services;

namespace TransparencyAudit.Api.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransparencyAuditApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TransparencyOptions>()
            .Bind(configuration.GetSection(TransparencyOptions.SectionName))
            .Validate(x =>
                string.Equals(x.AuthoritativeDigestSourceMode, "InMemory", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.AuthoritativeDigestSourceMode, "Http", StringComparison.OrdinalIgnoreCase),
                "Transparency:AuthoritativeDigestSourceMode must be InMemory or Http.")
            .Validate(x => x.AuthoritativeHttpTimeoutSeconds > 0, "Transparency:AuthoritativeHttpTimeoutSeconds must be greater than zero.")
            .Validate(x =>
                !string.Equals(x.AuthoritativeDigestSourceMode, "Http", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.MetadataDigestEndpointTemplate)
                    && !string.IsNullOrWhiteSpace(x.AuditDigestEndpointTemplate)
                    && x.MetadataDigestEndpointTemplate.Contains("{electionId}", StringComparison.OrdinalIgnoreCase)
                    && x.AuditDigestEndpointTemplate.Contains("{electionId}", StringComparison.OrdinalIgnoreCase)),
                "Transparency HTTP authoritative mode requires metadata/audit endpoint templates containing {electionId}.")
            .Validate(x =>
                string.Equals(x.SolanaReadMode, "ProjectionOnly", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.SolanaReadMode, "Mock", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.SolanaReadMode, "Http", StringComparison.OrdinalIgnoreCase),
                "Transparency:SolanaReadMode must be ProjectionOnly, Mock, or Http.")
            .Validate(x => x.SolanaHttpTimeoutSeconds > 0, "Transparency:SolanaHttpTimeoutSeconds must be greater than zero.")
            .Validate(x =>
                !string.Equals(x.SolanaReadMode, "Http", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.SolanaElectionStateEndpointTemplate)
                    && (x.SolanaElectionStateEndpointTemplate.Contains("{electionId}", StringComparison.OrdinalIgnoreCase)
                        || Uri.TryCreate(x.SolanaElectionStateEndpointTemplate, UriKind.Absolute, out _))),
                "Transparency HTTP Solana mode requires SolanaElectionStateEndpointTemplate to be an absolute URI or contain {electionId}.")
            .Validate(x => x.AutoReindexIntervalSeconds > 0, "Transparency:AutoReindexIntervalSeconds must be greater than zero.")
            .ValidateOnStart();
        services.AddOpenApi();

        services.AddHttpClient("TransparencyAuthoritativeDigests");
        services.AddHttpClient("TransparencyCanonicalSolanaReads");
        services.AddSingleton<IAuthoritativeEvidenceDigestSource>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<TransparencyOptions>>().Value;
            if (string.Equals(options.AuthoritativeDigestSourceMode, "Http", StringComparison.OrdinalIgnoreCase))
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient("TransparencyAuthoritativeDigests");
                httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.AuthoritativeHttpTimeoutSeconds));

                return new HttpAuthoritativeEvidenceDigestSource(
                    httpClient,
                    options.MetadataDigestEndpointTemplate,
                    options.AuditDigestEndpointTemplate);
            }

            return new InMemoryAuthoritativeEvidenceDigestSource(
                options.AuthoritativeMetadataDigests,
                options.AuthoritativeAuditDigests);
        });

        services.AddSingleton<ISolanaSettlementReadAdapter>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<TransparencyOptions>>().Value;
            if (string.Equals(options.SolanaReadMode, "Mock", StringComparison.OrdinalIgnoreCase))
            {
                return new MockSolanaSettlementReadAdapter(options.MockSolanaFinalizedStates);
            }

            if (string.Equals(options.SolanaReadMode, "Http", StringComparison.OrdinalIgnoreCase))
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = factory.CreateClient("TransparencyCanonicalSolanaReads");
                httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.SolanaHttpTimeoutSeconds));
                return new HttpSolanaSettlementReadAdapter(httpClient, options.SolanaElectionStateEndpointTemplate);
            }

            return new ProjectionOnlySolanaSettlementReadAdapter();
        });

        services.AddSingleton<IMetadataCorrelationService, MetadataCorrelationService>();
        services.AddSingleton<IAuditCorrelationService, AuditCorrelationService>();

        services.AddSingleton<IProjectionRepository>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<TransparencyOptions>>().Value;
            var configuredPath = options.ProjectionStorePath;

            var projectionPath = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChainBallot", "Transparency", "projections.json")
                : configuredPath;

            return new FileBackedProjectionRepository(projectionPath, sp.GetRequiredService<IConsistencyDigestCalculator>());
        });

        services.AddSingleton<IConsistencyDigestCalculator, Sha256ConsistencyDigestCalculator>();
        services.AddSingleton<IVerificationService, VerificationService>();
        services.AddSingleton<IResultSnapshotService, ResultSnapshotService>();
        services.AddSingleton<IProjectionManagementService, ProjectionManagementService>();
        services.AddSingleton<ICanonicalResyncEventSource, InMemoryCanonicalResyncEventSource>();
        services.AddSingleton<IReconciliationService, InMemoryReconciliationService>();
        services.AddSingleton<IAutoReindexOrchestrator, AutoReindexOrchestrator>();
        services.AddHostedService<ProjectionAutoReindexBackgroundService>();
        services.AddHostedService<CanonicalEventResyncBackgroundService>();

        services
            .AddAuthentication(TransparencyHeaderAuthenticationHandler.SchemeName)
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TransparencyHeaderAuthenticationHandler>(TransparencyHeaderAuthenticationHandler.SchemeName, _ => { });
        services.AddAuthorization();

        return services;
    }
}
