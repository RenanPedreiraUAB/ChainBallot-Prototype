using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VoterComponent.Web.Application;

namespace VoterComponent.Web.Tests;

public sealed class VoterOptionsValidationTests
{
    [Fact]
    public void AddVoterComponent_Throws_WhenMetadataTemplateHasNoElectionToken()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Voter:MetadataEndpointTemplate"] = "https://metadata.example/static.json"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddVoterComponent(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<VoterOptions>>().Value);
    }

    [Fact]
    public void AddVoterComponent_Throws_WhenKafkaModeWithoutBootstrapServers()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Voter:QueueMode"] = "Kafka",
                ["Voter:Kafka:BootstrapServers"] = ""
            })
            .Build();

        var services = new ServiceCollection();
        services.AddVoterComponent(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<VoterOptions>>().Value);
    }
}
