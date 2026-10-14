using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ThresholdDecryption.Api.Services;

namespace ThresholdDecryption.Api.Tests;

public sealed class ThresholdEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string OperatorHeaderName = "X-Operator-Key";
    private const string OperatorHeaderValue = "decryption-operator-key";

    private readonly WebApplicationFactory<Program> _factory;

    public ThresholdEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICanonicalElectionStateClient>();
                services.AddSingleton<ICanonicalElectionStateClient>(new StubCanonicalElectionStateClient(isFinalized: true));
            });
        });
    }

    [Fact]
    public async Task FullCeremonyFlow_CompletesAndPublishes()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(OperatorHeaderName, OperatorHeaderValue);

        var propose = await client.PostAsJsonAsync("/api/decryption/elections/election-10/ceremony", new
        {
            requiredShares = 2,
            requiredApprovals = 1,
            actor = "operator"
        });
        Assert.Equal(HttpStatusCode.OK, propose.StatusCode);

        var approve = await client.PostAsJsonAsync("/api/decryption/elections/election-10/ceremony/approve", new
        {
            approverId = "governor-a",
            note = "approved"
        });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var share1 = await client.PostAsJsonAsync("/api/decryption/elections/election-10/ceremony/share", new
        {
            trusteeId = "trustee-a",
            shareDigest = "share-a"
        });
        Assert.Equal(HttpStatusCode.OK, share1.StatusCode);

        var share2 = await client.PostAsJsonAsync("/api/decryption/elections/election-10/ceremony/share", new
        {
            trusteeId = "trustee-b",
            shareDigest = "share-b"
        });
        Assert.Equal(HttpStatusCode.OK, share2.StatusCode);

        var finalize = await client.PostAsJsonAsync("/api/decryption/elections/election-10/ceremony/finalize", new
        {
            actor = "operator",
            tallies = new Dictionary<string, long> { ["cand-a"] = 12, ["cand-b"] = 8 }
        });
        Assert.Equal(HttpStatusCode.OK, finalize.StatusCode);

        var publish = await client.PostAsJsonAsync("/api/decryption/elections/election-10/publish-final-tally", new
        {
            actor = "operator"
        });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var ceremony = await client.GetAsync("/api/decryption/elections/election-10/ceremony");
        Assert.Equal(HttpStatusCode.OK, ceremony.StatusCode);

        var body = await ceremony.Content.ReadAsStringAsync();
        Assert.Contains("Completed", body, StringComparison.Ordinal);
        Assert.Contains("threshold://", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProposeCeremony_RejectedWhenCanonicalStateIsNotClosed()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICanonicalElectionStateClient>();
                services.AddSingleton<ICanonicalElectionStateClient>(new StubCanonicalElectionStateClient(isFinalized: false));
            });
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(OperatorHeaderName, OperatorHeaderValue);

        var propose = await client.PostAsJsonAsync("/api/decryption/elections/election-not-closed/ceremony", new
        {
            requiredShares = 1,
            requiredApprovals = 1,
            actor = "operator"
        });

        Assert.Equal(HttpStatusCode.OK, propose.StatusCode);
        var body = await propose.Content.ReadAsStringAsync();
        Assert.Contains("Failed", body, StringComparison.Ordinal);
        Assert.Contains("not confirmed Closed/Finalized", body, StringComparison.Ordinal);
    }

    private sealed class StubCanonicalElectionStateClient(bool isFinalized) : ICanonicalElectionStateClient
    {
        public Task<CanonicalElectionStateDto?> GetElectionStateAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult<CanonicalElectionStateDto?>(new CanonicalElectionStateDto(electionId, isFinalized, "Test", DateTimeOffset.UtcNow, null));
    }
}
