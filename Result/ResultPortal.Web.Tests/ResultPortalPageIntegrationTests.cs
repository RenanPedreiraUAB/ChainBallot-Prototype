using System.Net;
using ChainBallot.PublicContracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ResultPortal.Web.Services;

namespace ResultPortal.Web.Tests;

public sealed class ResultPortalPageIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ResultPortalPageIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ResultPortal:AuthMode"] = "HeaderToken",
                    ["ResultPortal:HeaderAuthHeaderName"] = "X-ResultPortal-Auth",
                    ["ResultPortal:HeaderAuthToken"] = "test-token",
                    ["ResultPortal:HeaderAuthRoles:0"] = "result-reader",
                    ["ResultPortal:DefaultElectionVisibility"] = "Public",
                    ["ResultPortal:PrivateElectionIds:0"] = "private-1",
                    ["ResultPortal:PrivateElectionViewerRole"] = "result-reader",
                    ["ResultPortal:HidePrivateElectionExistence"] = "true"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITransparencyReadClient>();
                services.AddSingleton<ITransparencyReadClient, StubTransparencyReadClient>();
            });
        });
    }

    [Fact]
    public async Task ResultsPage_RedirectsToOidcChallenge_WhenOidcModeAndPrivateElectionWithoutAuthentication()
    {
        var oidcFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ResultPortal:AuthMode"] = "Oidc",
                    ["ResultPortal:OidcAuthority"] = "https://idp.example",
                    ["ResultPortal:OidcClientId"] = "result-portal",
                    ["ResultPortal:OidcClientSecret"] = "secret",
                    ["ResultPortal:OidcRequireHttpsMetadata"] = "false",
                    ["ResultPortal:DefaultElectionVisibility"] = "Public",
                    ["ResultPortal:PrivateElectionIds:0"] = "private-oidc",
                    ["ResultPortal:PrivateElectionViewerRole"] = "result-reader",
                    ["ResultPortal:HidePrivateElectionExistence"] = "false"
                });
            });
        });

        using var client = oidcFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Results?ElectionId=private-oidc");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("https://idp.example", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResultsPage_ReturnsNotFound_WhenPrivateElectionIsRequestedWithoutAuthentication()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Results?ElectionId=private-1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResultsPage_ReturnsSnapshot_WhenPrivateElectionIsRequestedWithValidHeaderAuth()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-ResultPortal-Auth", "test-token");

        var response = await client.GetAsync("/Results?ElectionId=private-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("private-1", html, StringComparison.Ordinal);
        Assert.Contains("digest-private-1", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyPage_ShowsNotFoundMessage_WhenVerificationRecordDoesNotExist()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Verify?ElectionId=public-1&Vid=missing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Verification response was not found", html, StringComparison.Ordinal);
    }

    private sealed class StubTransparencyReadClient : ITransparencyReadClient
    {
        public Task<TransparencyReadResult<VerifiedResultSnapshot>> GetSnapshotAsync(string electionId, CancellationToken cancellationToken)
        {
            if (string.Equals(electionId, "private-1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(electionId, "public-1", StringComparison.OrdinalIgnoreCase))
            {
                var snapshot = new VerifiedResultSnapshot(
                    electionId,
                    $"digest-{electionId}",
                    "Finalized",
                    new SettlementReference("Localnet", "program-1", electionId, "tx-1", 42, DateTimeOffset.UtcNow, "finalized"),
                    new Dictionary<string, long> { ["cand-a"] = 11 },
                    DateTimeOffset.UtcNow);

                return Task.FromResult(TransparencyReadResult<VerifiedResultSnapshot>.Success(snapshot, "test-correlation-id"));
            }

            return Task.FromResult(TransparencyReadResult<VerifiedResultSnapshot>.NotFound("test-correlation-id"));
        }

        public Task<TransparencyReadResult<VidVerificationResponse>> VerifyByVidAsync(string electionId, string vid, CancellationToken cancellationToken)
        {
            if (string.Equals(electionId, "public-1", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(vid, "vid-123", StringComparison.OrdinalIgnoreCase))
            {
                var response = new VidVerificationResponse(
                    electionId,
                    vid,
                    VerificationOutcome.Verified,
                    "VID verification succeeded.",
                    new SettlementReference("Localnet", "program-1", electionId, "tx-1", 42, DateTimeOffset.UtcNow, "finalized"),
                    new InclusionProofReference("batch-1", "root-1", "leaf-1", 0, "proof://1", "hash-1"),
                    DateTimeOffset.UtcNow,
                    "consistency-1");

                return Task.FromResult(TransparencyReadResult<VidVerificationResponse>.Success(response, "test-correlation-id"));
            }

            return Task.FromResult(TransparencyReadResult<VidVerificationResponse>.NotFound("test-correlation-id"));
        }
    }
}
