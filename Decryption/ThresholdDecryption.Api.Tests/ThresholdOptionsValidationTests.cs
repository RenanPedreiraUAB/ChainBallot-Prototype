using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ThresholdDecryption.Api.Application;

namespace ThresholdDecryption.Api.Tests;

public sealed class ThresholdOptionsValidationTests
{
    [Fact]
    public void AddThresholdDecryptionApi_Throws_WhenRequiredSharesExceedAllowedTrustees()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ThresholdDecryption:DefaultRequiredShares"] = "6",
                ["ThresholdDecryption:DefaultRequiredApprovals"] = "4"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddThresholdDecryptionApi(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<ThresholdOptions>>().Value);
    }

    [Fact]
    public void AddThresholdDecryptionApi_Throws_WhenOperatorKeyMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ThresholdDecryption:OperatorAccessKey"] = ""
            })
            .Build();

        var services = new ServiceCollection();
        services.AddThresholdDecryptionApi(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<ThresholdOptions>>().Value);
    }
}
