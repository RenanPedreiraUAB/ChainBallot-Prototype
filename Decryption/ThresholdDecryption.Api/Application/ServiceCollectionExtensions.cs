using Microsoft.Extensions.Options;
using ThresholdDecryption.Api.Services;

namespace ThresholdDecryption.Api.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddThresholdDecryptionApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ThresholdOptions>()
            .Bind(configuration.GetSection(ThresholdOptions.SectionName))
            .Validate(x => x.DefaultRequiredShares > 0, "ThresholdDecryption:DefaultRequiredShares must be greater than zero.")
            .Validate(x => x.DefaultRequiredApprovals > 0, "ThresholdDecryption:DefaultRequiredApprovals must be greater than zero.")
            .Validate(x => (x.AllowedTrustees?.Length ?? 0) >= x.DefaultRequiredShares,
                "ThresholdDecryption requires AllowedTrustees count >= DefaultRequiredShares.")
            .Validate(x => (x.GovernanceApprovers?.Length ?? 0) >= x.DefaultRequiredApprovals,
                "ThresholdDecryption requires GovernanceApprovers count >= DefaultRequiredApprovals.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.OperatorAccessHeaderName), "ThresholdDecryption:OperatorAccessHeaderName is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.OperatorAccessKey), "ThresholdDecryption:OperatorAccessKey is required.")
            .Validate(x => Uri.TryCreate(x.TransparencyBaseUrl, UriKind.Absolute, out _), "ThresholdDecryption:TransparencyBaseUrl must be an absolute URI.")
            .ValidateOnStart();
        services.AddOpenApi();

        services.AddSingleton<IThresholdCeremonyRepository>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ThresholdOptions>>().Value;
            var path = string.IsNullOrWhiteSpace(options.StorePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChainBallot", "Decryption", "ceremonies.json")
                : options.StorePath;
            return new FileBackedThresholdCeremonyRepository(path);
        });

        services.AddHttpClient<ICanonicalElectionStateClient, HttpCanonicalElectionStateClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ThresholdOptions>>().Value;
            client.BaseAddress = new Uri(options.TransparencyBaseUrl);
        });

        services.AddSingleton<IThresholdDecryptionService, ThresholdDecryptionService>();

        return services;
    }
}
