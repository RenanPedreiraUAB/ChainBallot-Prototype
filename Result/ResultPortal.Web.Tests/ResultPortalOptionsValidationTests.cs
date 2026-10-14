using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ResultPortal.Web.Application;

namespace ResultPortal.Web.Tests;

public sealed class ResultPortalOptionsValidationTests
{
    [Fact]
    public void AddResultPortal_Throws_WhenOidcModeHasNoAuthority()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ResultPortal:AuthMode"] = "Oidc",
                ["ResultPortal:OidcClientId"] = "result-portal",
                ["ResultPortal:OidcScopes:0"] = "openid"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddResultPortal(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<ResultPortalOptions>>().Value);
    }
}
