using ElectionProcessing.Api.Application;
using ElectionProcessing.Api.Processing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ElectionProcessing.Api.Tests;

public sealed class ProcessingOptionsValidationTests
{
    [Fact]
    public void AddElectionProcessingCore_Throws_WhenKafkaModeMissingTopic()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Processing:QueueMode"] = QueueModes.Kafka,
                ["Processing:Kafka:BootstrapServers"] = "localhost:9092",
                ["Processing:Kafka:Topic"] = "",
                ["Processing:Kafka:ConsumerGroupId"] = "chainballot-tests",
                ["Processing:MaxBallotsPerBatch"] = "1",
                ["Processing:MaxBatchWaitSeconds"] = "1",
                ["Processing:PollIntervalMilliseconds"] = "1"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddElectionProcessingCore(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<ProcessingOptions>>().Value);
    }

    [Fact]
    public void ProcessingSchemaContracts_ArePinnedToVersionOne()
    {
        Assert.Equal(1, ProcessingSchemaContracts.BatchRecordSchemaVersion);
        Assert.Equal(1, ProcessingSchemaContracts.TallyRecordSchemaVersion);
        Assert.Equal(1, ProcessingSchemaContracts.ProofArtifactSchemaVersion);
    }

    [Fact]
    public void AddElectionProcessingCore_Throws_WhenProjectionPublishingEnabledWithInvalidBaseUrl()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Processing:ProjectionPublishing:Enabled"] = "true",
                ["Processing:ProjectionPublishing:BaseUrl"] = "not-a-url"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddElectionProcessingCore(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<ProcessingOptions>>().Value);
    }

    [Fact]
    public void HttpProjectionEvidencePublisher_Client_SendsConfiguredAuthHeader()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Processing:ProjectionPublishing:Enabled"] = "true",
                ["Processing:ProjectionPublishing:BaseUrl"] = "http://transparency:5264",
                ["Processing:ProjectionPublishing:AuthHeaderName"] = "X-Transparency-Auth",
                ["Processing:ProjectionPublishing:AuthToken"] = "transparency-ingestion-dev-token"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddElectionProcessingCore(configuration);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient(nameof(HttpProjectionEvidencePublisher));

        Assert.True(client.DefaultRequestHeaders.Contains("X-Transparency-Auth"));
        Assert.Equal("transparency-ingestion-dev-token", client.DefaultRequestHeaders.GetValues("X-Transparency-Auth").Single());
    }
}
