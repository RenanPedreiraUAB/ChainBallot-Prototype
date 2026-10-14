using Microsoft.Extensions.Options;
using WatcherAudit.Api.Services;

namespace WatcherAudit.Api.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWatcherAuditApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WatcherOptions>()
            .Bind(configuration.GetSection(WatcherOptions.SectionName))
            .Validate(x => Uri.TryCreate(x.TransparencyBaseUrl, UriKind.Absolute, out _), "Watcher:TransparencyBaseUrl must be an absolute URI.")
            .Validate(x => x.TransparencyTimeoutSeconds > 0, "Watcher:TransparencyTimeoutSeconds must be greater than zero.")
            .Validate(x =>
                string.Equals(x.DigestSourceMode, "InMemory", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.DigestSourceMode, "Http", StringComparison.OrdinalIgnoreCase),
                "Watcher:DigestSourceMode must be InMemory or Http.")
            .Validate(x => x.DigestHttpTimeoutSeconds > 0, "Watcher:DigestHttpTimeoutSeconds must be greater than zero.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.OperatorAccessHeaderName), "Watcher:OperatorAccessHeaderName is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.OperatorAccessKey), "Watcher:OperatorAccessKey is required.")
            .Validate(x =>
                !string.Equals(x.DigestSourceMode, "Http", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.MetadataDigestEndpointTemplate)
                    && !string.IsNullOrWhiteSpace(x.AuditDigestEndpointTemplate)
                    && x.MetadataDigestEndpointTemplate.Contains("{electionId}", StringComparison.OrdinalIgnoreCase)
                    && x.AuditDigestEndpointTemplate.Contains("{electionId}", StringComparison.OrdinalIgnoreCase)),
                "Watcher HTTP digest mode requires metadata/audit endpoint templates containing {electionId}.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.ReportSigningKey), "Watcher:ReportSigningKey is required.")
            .ValidateOnStart();
        services.AddOpenApi();

        services.AddHttpClient("WatcherTransparency");
        services.AddHttpClient("WatcherAuthoritativeDigests");

        services.AddSingleton<ITransparencyAuditReadClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<WatcherOptions>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("WatcherTransparency");
            var baseUrl = string.IsNullOrWhiteSpace(options.TransparencyBaseUrl)
                ? "http://localhost:5284"
                : options.TransparencyBaseUrl;

            if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
            {
                baseUrl += "/";
            }

            httpClient.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TransparencyTimeoutSeconds));
            return new HttpTransparencyAuditReadClient(httpClient);
        });

        services.AddSingleton<IAuthoritativeDigestSource>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<WatcherOptions>>().Value;
            if (string.Equals(options.DigestSourceMode, "Http", StringComparison.OrdinalIgnoreCase))
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("WatcherAuthoritativeDigests");
                httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.DigestHttpTimeoutSeconds));
                return new HttpAuthoritativeDigestSource(httpClient, options.MetadataDigestEndpointTemplate, options.AuditDigestEndpointTemplate);
            }

            return new InMemoryAuthoritativeDigestSource(options.MetadataDigests, options.AuditDigests);
        });

        services.AddSingleton<IWatcherDigestCalculator, Sha256WatcherDigestCalculator>();
        services.AddSingleton<IWatcherReportSigner, HmacWatcherReportSigner>();
        services.AddSingleton<IWatcherReportRepository>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<WatcherOptions>>().Value;
            var reportPath = string.IsNullOrWhiteSpace(options.ReportStorePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChainBallot", "Watcher", "reports.json")
                : options.ReportStorePath;

            return new FileBackedWatcherReportRepository(reportPath);
        });
        services.AddSingleton<IWatcherAuditService, WatcherAuditService>();

        return services;
    }
}
