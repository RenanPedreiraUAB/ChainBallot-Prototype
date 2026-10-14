using System.Net;
using System.Net.Http;
using System.Text;
using TransparencyAudit.Api.Services;

namespace TransparencyAudit.Api.Tests;

public sealed class HttpSolanaSettlementReadAdapterTests
{
    [Fact]
    public async Task GetElectionStateAsync_ReturnsNull_OnRequestTimeoutLikeFailure()
    {
        using var httpClient = new HttpClient(new ThrowingHandler(new TaskCanceledException("timeout")));
        var adapter = new HttpSolanaSettlementReadAdapter(httpClient, "https://solana.test/elections/{electionId}");

        var state = await adapter.GetElectionStateAsync("election-1", CancellationToken.None);

        Assert.Null(state);
    }

    [Fact]
    public async Task GetElectionStateAsync_ParsesFinalizedPayload_WhenHttpSucceeds()
    {
        using var httpClient = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"isFinalized\":true,\"note\":\"ok\"}", Encoding.UTF8, "application/json")
            }));
        var adapter = new HttpSolanaSettlementReadAdapter(httpClient, "https://solana.test/elections/{electionId}");

        var state = await adapter.GetElectionStateAsync("election-1", CancellationToken.None);

        Assert.NotNull(state);
        Assert.True(state!.IsFinalized);
        Assert.Equal("HttpSolana", state.Source);
    }

    private sealed class ThrowingHandler(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(ex);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
