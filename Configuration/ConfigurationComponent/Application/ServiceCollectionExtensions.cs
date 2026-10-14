using ConfigurationComponent.Blockchain;
using ConfigurationComponent.Infrastructure;
using ConfigurationComponent.Repositories;
using ConfigurationComponent.Services;
using ConfigurationComponent.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConfigurationComponent.Application;

/// <summary>
/// Represents Service Collection Extensions.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Executes Add Configuration Component.
    /// </summary>
    public static IServiceCollection AddConfigurationComponent(this IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables(prefix: "CHAINBALLOT_")
            .Build();

        var defaults = new ProvisioningDefaults();
        configuration.GetSection("ProvisioningDefaults").Bind(defaults);

        var solanaOptions = new SolanaOptions();
        configuration.GetSection("Solana").Bind(solanaOptions);

        services.AddSingleton(defaults);
        services.AddSingleton(solanaOptions);

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddDebug();
            builder.AddConsole();
        });

        services.AddSingleton<IElectionRepository, FileElectionRepository>();
        services.AddSingleton<IDeploymentManifestRepository, FileDeploymentManifestRepository>();

        services.AddSingleton<IMetadataRepository, HttpMetadataRepository>();

        if (solanaOptions.Mode == SolanaServiceMode.Rpc)
        {
            services.AddSingleton<ISolanaService, RealSolanaService>();
        }
        else
        {
            services.AddSingleton<ISolanaService, SolanaService>();
        }

        services.AddSingleton<IElectionService, ElectionService>();
        services.AddSingleton<ICandidateService, CandidateService>();
        services.AddSingleton<IMetadataGenerator, MetadataGenerator>();
        services.AddSingleton<IMetadataHasher, MetadataHasher>();
        services.AddSingleton<IDeploymentManifestGenerator, DeploymentManifestGenerator>();
        services.AddSingleton<IDeploymentService, DeploymentService>();
        services.AddSingleton<IRecommendationService, RecommendationService>();
        services.AddSingleton<IValidationService, ValidationService>();

        services.AddTransient<MainWindowViewModel>();

        return services;
    }
}
