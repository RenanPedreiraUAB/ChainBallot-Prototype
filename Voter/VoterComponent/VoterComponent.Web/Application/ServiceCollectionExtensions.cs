using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using VoterComponent.Web.Services;

namespace VoterComponent.Web.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVoterComponent(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<VoterOptions>()
            .Bind(configuration.GetSection(VoterOptions.SectionName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.CookieName), "Voter:CookieName is required.")
            .Validate(x => Uri.TryCreate(x.CynetAuthBaseUrl, UriKind.Absolute, out _), "Voter:CynetAuthBaseUrl must be an absolute URI.")
            .Validate(x => Uri.TryCreate(x.ElectionAuthorizationBaseUrl, UriKind.Absolute, out _), "Voter:ElectionAuthorizationBaseUrl must be an absolute URI.")
            .Validate(x =>
                    string.Equals(x.QueueMode, VoterQueueModes.Kafka, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.QueueMode, VoterQueueModes.InMemory, StringComparison.OrdinalIgnoreCase),
                "Voter:QueueMode must be Kafka or InMemory.")
            .Validate(x =>
                    !string.Equals(x.QueueMode, VoterQueueModes.Kafka, StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(x.Kafka.BootstrapServers),
                "Voter:Kafka:BootstrapServers is required when QueueMode is Kafka.")
            .Validate(x =>
                    !string.Equals(x.QueueMode, VoterQueueModes.Kafka, StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(x.Kafka.Topic),
                "Voter:Kafka:Topic is required when QueueMode is Kafka.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.MetadataEndpointTemplate)
                          && x.MetadataEndpointTemplate.Contains("{electionId}", StringComparison.OrdinalIgnoreCase),
                "Voter:MetadataEndpointTemplate must contain {electionId}.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.DefaultElectionId), "Voter:DefaultElectionId is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.BallotEncryptionKey), "Voter:BallotEncryptionKey is required.")
            .Validate(x => x.SubmissionOutboxEntryTtlMinutes > 0, "Voter:SubmissionOutboxEntryTtlMinutes must be greater than zero.")
            .Validate(x => x.SubmissionOutboxMaxRetryCount > 0, "Voter:SubmissionOutboxMaxRetryCount must be greater than zero.")
            .Validate(x => x.SubmissionOutboxMaxEntries > 0, "Voter:SubmissionOutboxMaxEntries must be greater than zero.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                var voterOptions = configuration.GetSection(VoterOptions.SectionName).Get<VoterOptions>() ?? new VoterOptions();

                options.Cookie.Name = voterOptions.CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.LoginPath = "/Login";
                options.LogoutPath = "/Logout";
                options.AccessDeniedPath = "/Login";
            });

        services.AddAuthorization();

        services.AddRazorPages(options =>
        {
            options.Conventions.AllowAnonymousToPage("/Index");
            options.Conventions.AllowAnonymousToPage("/Login");
            options.Conventions.AllowAnonymousToPage("/Privacy");
            options.Conventions.AllowAnonymousToPage("/Error");
            options.Conventions.AuthorizePage("/Dashboard");
            options.Conventions.AuthorizePage("/Logout");
        });

        services.AddHttpClient<IAuthProvider, CynetAuthAuthProvider>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<VoterOptions>>().Value;
            client.BaseAddress = new Uri(options.CynetAuthBaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHttpClient<IElectionAuthorizationClient, ElectionAuthorizationClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<VoterOptions>>().Value;
            client.BaseAddress = new Uri(options.ElectionAuthorizationBaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHttpClient<IElectionMetadataClient, ElectionMetadataClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<VoterOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.MetadataEndpointTemplate))
            {
                var normalized = options.MetadataEndpointTemplate.Replace("{electionId}", string.Empty, StringComparison.Ordinal);
                client.BaseAddress = new Uri(normalized, UriKind.Absolute);
            }

            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddSingleton<InMemoryBallotQueuePublisher>();
        services.AddSingleton<KafkaBallotQueuePublisher>();
        services.AddSingleton<IQueuePublisher>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<VoterOptions>>().Value;
            return string.Equals(options.QueueMode, VoterQueueModes.InMemory, StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<InMemoryBallotQueuePublisher>()
                : sp.GetRequiredService<KafkaBallotQueuePublisher>();
        });
        services.AddSingleton<IBallotSubmissionOutbox>(sp =>
        {
            var voterOptions = sp.GetRequiredService<IOptions<VoterOptions>>().Value;
            var configuredPath = voterOptions.SubmissionOutboxPath;
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                configuredPath = Path.Combine(baseDir, "ChainBallot", "VoterComponent", "submission-outbox.json");
            }

            return new FileBackedBallotSubmissionOutbox(configuredPath);
        });
        services.AddSingleton<IBallotEncryptionService, AesGcmBallotEncryptionService>();
        services.AddSingleton<IBallotCommitmentService, Sha256BallotCommitmentService>();
        services.AddScoped<IVoterWorkflowService, VoterWorkflowService>();

        return services;
    }
}