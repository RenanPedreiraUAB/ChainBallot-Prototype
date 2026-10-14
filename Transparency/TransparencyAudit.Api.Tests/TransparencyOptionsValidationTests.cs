using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TransparencyAudit.Api.Application;

namespace TransparencyAudit.Api.Tests;

public sealed class TransparencyOptionsValidationTests
{
    [Fact]
    public void AddTransparencyAuditApi_Throws_WhenHttpDigestModeMissingTemplates()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Transparency:AuthoritativeDigestSourceMode"] = "Http",
                ["Transparency:MetadataDigestEndpointTemplate"] = "",
                ["Transparency:AuditDigestEndpointTemplate"] = ""
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTransparencyAuditApi(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<TransparencyOptions>>().Value);
    }
}
