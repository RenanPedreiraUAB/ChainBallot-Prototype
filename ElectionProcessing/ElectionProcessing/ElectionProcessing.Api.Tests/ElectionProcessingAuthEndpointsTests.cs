using System.Net;
using System.Net.Http.Json;
using ChainBallot.PublicContracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ElectionProcessing.Api.Tests;

/// <summary>Covers Part F: authentication/authorization on Processing's privileged operational routes.</summary>
public sealed class ElectionProcessingAuthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string HeaderName = "X-Processing-Auth";
    private const string OperatorToken = "test-operator-token";
    private const string IngestToken = "test-ingest-token";

    private readonly WebApplicationFactory<Program> _factory;

    public ElectionProcessingAuthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Processing:QueueMode"] = "InMemory",
                    ["Processing:SettlementMode"] = "Mock",
                    ["Processing:ProjectionPublishing:Enabled"] = "false",
                    ["Processing:Authentication:HeaderName"] = HeaderName,
                    ["Processing:Authentication:Credentials:0:Token"] = OperatorToken,
                    ["Processing:Authentication:Credentials:0:Name"] = "test-operator",
                    ["Processing:Authentication:Credentials:0:Roles:0"] = "processing.operator",
                    ["Processing:Authentication:Credentials:1:Token"] = IngestToken,
                    ["Processing:Authentication:Credentials:1:Name"] = "test-ingest",
                    ["Processing:Authentication:Credentials:1:Roles:0"] = "processing.ingest"
                });
            });
        });
    }

    [Theory]
    [InlineData("/api/processing/elections/election-auth-unauth/pause")]
    [InlineData("/api/processing/elections/election-auth-unauth/resume")]
    [InlineData("/api/processing/elections/election-auth-unauth/retry-failed-batch")]
    public async Task PrivilegedOperatorRoute_WithoutCredentials_IsRejected(string route)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync(route, content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/processing/elections/election-auth-wrongrole/pause")]
    [InlineData("/api/processing/elections/election-auth-wrongrole/resume")]
    [InlineData("/api/processing/elections/election-auth-wrongrole/retry-failed-batch")]
    public async Task PrivilegedOperatorRoute_WithIngestRole_IsRejected(string route)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(HeaderName, IngestToken);

        var response = await client.PostAsync(route, content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PauseAndResume_WithOperatorRole_Succeeds()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(HeaderName, OperatorToken);

        var pauseResponse = await client.PostAsync("/api/processing/elections/election-auth-ok/pause", content: null);
        Assert.Equal(HttpStatusCode.OK, pauseResponse.StatusCode);

        var resumeResponse = await client.PostAsync("/api/processing/elections/election-auth-ok/resume", content: null);
        Assert.Equal(HttpStatusCode.OK, resumeResponse.StatusCode);
    }

    [Fact]
    public async Task PauseElection_CalledTwiceByOperator_SecondCallIsSafeNoOp()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(HeaderName, OperatorToken);

        var first = await client.PostAsync("/api/processing/elections/election-auth-repeat/pause", content: null);
        var second = await client.PostAsync("/api/processing/elections/election-auth-repeat/pause", content: null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("\"changed\":true", await first.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"changed\":false", await second.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ingest_WithoutCredentials_IsRejected()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/processing/ingest", CreatePackage());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithOperatorRole_IsRejected()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(HeaderName, OperatorToken);

        var response = await client.PostAsJsonAsync("/api/processing/ingest", CreatePackage());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithIngestRole_Succeeds()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(HeaderName, IngestToken);

        var response = await client.PostAsJsonAsync("/api/processing/ingest", CreatePackage());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private static BallotSubmissionPackage CreatePackage()
    {
        const string electionId = "election-auth-ingest";
        const string vid = "vid_0123456789abcdef0123456789abcdef0123";
        var nonce = Guid.NewGuid().ToString("N");
        var nullifier = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{electionId}|{vid}|{nonce}"))).ToLowerInvariant();
        var commitment = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant();

        return new BallotSubmissionPackage(
            ElectionId: electionId,
            Vid: vid,
            BallotCommitment: commitment,
            SubmissionNonce: nonce,
            BallotNullifier: nullifier,
            EncryptedVotePayload: new EncryptedBallotPayload("A256GCM", "bm9uY2U=", "dGFn", "Y2lwaGVy"),
            CreatedAtUtc: DateTimeOffset.UtcNow);
    }
}
