using System.Net;
using System.Net.Http;
using System.Text;
using ChainBallot.PublicContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ResultPortal.Web.Services;

namespace ResultPortal.Web.Tests;

public sealed class TransparencyReadClientTests
{
    [Fact]
    public async Task GetSnapshotAsync_ReturnsSnapshot_WhenApiReturns200()
    {
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """
                    {
                      "electionId": "election-1",
                      "resultDigest": "digest-1",
                      "lifecycleState": "Finalized",
                      "settlementReference": {
                        "network": "Localnet",
                        "programId": "program-1",
                        "electionId": "election-1",
                        "transactionSignature": "tx-1",
                        "slot": 100,
                        "blockTimeUtc": "2026-07-13T00:00:00Z",
                        "commitment": "finalized"
                      },
                      "candidateTallies": {
                        "cand-a": 10
                      },
                      "generatedAtUtc": "2026-07-13T00:00:00Z"
                    }
                    """)
            }))
        {
            BaseAddress = new Uri("http://localhost:5284/")
        };

        var sut = new TransparencyReadClient(client, BuildHttpContextAccessor(), NullLogger<TransparencyReadClient>.Instance);

        var readResult = await sut.GetSnapshotAsync("election-1", CancellationToken.None);

        Assert.Equal(TransparencyReadStatus.Success, readResult.Status);
        Assert.NotNull(readResult.Value);
        Assert.Equal("election-1", readResult.Value!.ElectionId);
        Assert.Equal("Finalized", readResult.Value.LifecycleState);
        Assert.Equal(10, readResult.Value.CandidateTallies["cand-a"]);
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsNull_WhenApiReturns404()
    {
        using var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)))
        {
            BaseAddress = new Uri("http://localhost:5284/")
        };

        var sut = new TransparencyReadClient(client, BuildHttpContextAccessor(), NullLogger<TransparencyReadClient>.Instance);

        var readResult = await sut.GetSnapshotAsync("missing", CancellationToken.None);

        Assert.Equal(TransparencyReadStatus.NotFound, readResult.Status);
        Assert.Null(readResult.Value);
    }

    [Fact]
    public async Task VerifyByVidAsync_ReturnsResponse_WhenApiReturns200()
    {
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """
                    {
                      "electionId": "election-1",
                      "vid": "vid-123",
                      "outcome": 0,
                      "message": "VID verification succeeded.",
                      "settlementReference": {
                        "network": "Localnet",
                        "programId": "program-1",
                        "electionId": "election-1",
                        "transactionSignature": "tx-1",
                        "slot": 100,
                        "blockTimeUtc": "2026-07-13T00:00:00Z",
                        "commitment": "finalized"
                      },
                      "inclusionProofReference": {
                        "batchId": "batch-1",
                        "merkleRoot": "root-1",
                        "leafCommitment": "commitment-1",
                        "leafIndex": 0,
                        "proofUri": "proof://1",
                        "proofHash": "hash-1"
                      },
                      "checkedAtUtc": "2026-07-13T00:00:00Z",
                      "consistencyDigest": "consistency-1"
                    }
                    """)
            }))
        {
            BaseAddress = new Uri("http://localhost:5284/")
        };

        var sut = new TransparencyReadClient(client, BuildHttpContextAccessor(), NullLogger<TransparencyReadClient>.Instance);

        var readResult = await sut.VerifyByVidAsync("election-1", "vid-123", CancellationToken.None);

        Assert.Equal(TransparencyReadStatus.Success, readResult.Status);
        Assert.NotNull(readResult.Value);
        Assert.Equal(VerificationOutcome.Verified, readResult.Value!.Outcome);
        Assert.Equal("vid-123", readResult.Value.Vid);
        Assert.NotNull(readResult.Value.InclusionProofReference);
    }

    private static StringContent JsonContent(string json)
        => new(json, Encoding.UTF8, "application/json");

    private static IHttpContextAccessor BuildHttpContextAccessor()
        => new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = "test-correlation-id"
            }
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
