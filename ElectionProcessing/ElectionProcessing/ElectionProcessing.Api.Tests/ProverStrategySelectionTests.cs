using ElectionProcessing.Api.Application;
using ElectionProcessing.Api.Processing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElectionProcessing.Api.Tests;

public sealed class ProverStrategySelectionTests
{
    [Fact]
    public void AddElectionProcessingCore_DefaultsToDeterministicStubProver()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Processing:MaxBallotsPerBatch"] = "32",
                ["Processing:MaxBatchWaitSeconds"] = "20",
                ["Processing:PollIntervalMilliseconds"] = "1000"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddElectionProcessingCore(configuration);

        using var provider = services.BuildServiceProvider();
        var strategy = provider.GetRequiredService<IZkProofStrategy>();

        Assert.IsType<DeterministicStubZkProofStrategy>(strategy);
    }

    [Fact]
    public void AddElectionProcessingCore_CanSwitchToExternalHttpProver()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Processing:MaxBallotsPerBatch"] = "32",
                ["Processing:MaxBatchWaitSeconds"] = "20",
                ["Processing:PollIntervalMilliseconds"] = "1000",
                ["Processing:ProverMode"] = ProverModes.ExternalHttp,
                ["Processing:ExternalProver:Endpoint"] = "http://localhost:5057/api/prover/generate",
                ["Processing:ExternalProver:TimeoutSeconds"] = "30"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddElectionProcessingCore(configuration);

        using var provider = services.BuildServiceProvider();
        var strategy = provider.GetRequiredService<IZkProofStrategy>();

        Assert.IsType<ExternalHttpZkProofStrategy>(strategy);
    }

    [Fact]
    public void AddElectionProcessingCore_CanSwitchToLocalProcessProver()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Processing:MaxBallotsPerBatch"] = "32",
                ["Processing:MaxBatchWaitSeconds"] = "20",
                ["Processing:PollIntervalMilliseconds"] = "1000",
                ["Processing:ProverMode"] = ProverModes.LocalProcess,
                ["Processing:LocalProcessProver:ExecutablePath"] = "C:/tools/prover.exe",
                ["Processing:LocalProcessProver:TimeoutSeconds"] = "60"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddElectionProcessingCore(configuration);

        using var provider = services.BuildServiceProvider();
        var strategy = provider.GetRequiredService<IZkProofStrategy>();

        Assert.IsType<LocalProcessZkProofStrategy>(strategy);
    }

    [Fact]
    public void AddElectionProcessingCore_UsesDeterministicVerifierInStubMode()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Processing:MaxBallotsPerBatch"] = "32",
                ["Processing:MaxBatchWaitSeconds"] = "20",
                ["Processing:PollIntervalMilliseconds"] = "1000",
                ["Processing:ProverMode"] = ProverModes.DeterministicStub
            })
            .Build();

        var services = new ServiceCollection();
        services.AddElectionProcessingCore(configuration);

        using var provider = services.BuildServiceProvider();
        var verifier = provider.GetRequiredService<IProofArtifactVerifier>();

        Assert.IsType<DeterministicStubProofArtifactVerifier>(verifier);
    }

    [Fact]
    public void AddElectionProcessingCore_UsesBasicVerifierForNonStubModes()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Processing:MaxBallotsPerBatch"] = "32",
                ["Processing:MaxBatchWaitSeconds"] = "20",
                ["Processing:PollIntervalMilliseconds"] = "1000",
                ["Processing:ProverMode"] = ProverModes.ExternalHttp,
                ["Processing:ExternalProver:Endpoint"] = "http://localhost:5057/api/prover/generate",
                ["Processing:ExternalProver:TimeoutSeconds"] = "30"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddElectionProcessingCore(configuration);

        using var provider = services.BuildServiceProvider();
        var verifier = provider.GetRequiredService<IProofArtifactVerifier>();

        Assert.IsType<BasicProofArtifactVerifier>(verifier);
    }
}
