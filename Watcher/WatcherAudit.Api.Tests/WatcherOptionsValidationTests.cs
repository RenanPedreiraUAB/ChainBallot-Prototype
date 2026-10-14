using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WatcherAudit.Api.Application;

namespace WatcherAudit.Api.Tests;

public sealed class WatcherOptionsValidationTests
{
    [Fact]
    public void AddWatcherAuditApi_Throws_WhenHttpDigestModeMissingTemplates()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Watcher:DigestSourceMode"] = "Http",
                ["Watcher:MetadataDigestEndpointTemplate"] = "",
                ["Watcher:AuditDigestEndpointTemplate"] = ""
            })
            .Build();

        var services = new ServiceCollection();
        services.AddWatcherAuditApi(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<WatcherOptions>>().Value);
    }

    [Fact]
    public void AddWatcherAuditApi_Throws_WhenOperatorKeyMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Watcher:OperatorAccessKey"] = ""
            })
            .Build();

        var services = new ServiceCollection();
        services.AddWatcherAuditApi(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<WatcherOptions>>().Value);
    }
}
