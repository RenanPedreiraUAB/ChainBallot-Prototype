using System.Net;
using System.Net.Http;
using System.Text;
using TransparencyAudit.Api.Services;

namespace TransparencyAudit.Api.Tests;

public sealed class HttpAuthoritativeEvidenceDigestSourceTests
{
    [Fact]
    public async Task GetMetadataDigestAsync_ReturnsDigest_FromJsonPayload()
    {
        using var httpClient = new HttpClient(new StubHandler(request =>
        {
            Assert.Equal("https://meta.test/elections/election-1/digest", request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"digest\":\"meta-123\"}", Encoding.UTF8, "application/json")
            };
        }));

        var source = new HttpAuthoritativeEvidenceDigestSource(
            httpClient,
            "https://meta.test/elections/{electionId}/digest",
            "https://audit.test/elections/{electionId}/digest");

        var digest = await source.GetMetadataDigestAsync("election-1", CancellationToken.None);

        Assert.Equal("meta-123", digest);
    }

    [Fact]
    public async Task GetAuditDigestAsync_ReturnsNull_OnNonSuccessStatus()
    {
        using var httpClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        var source = new HttpAuthoritativeEvidenceDigestSource(
            httpClient,
            "https://meta.test/elections/{electionId}/digest",
            "https://audit.test/elections/{electionId}/digest");

        var digest = await source.GetAuditDigestAsync("missing", CancellationToken.None);

        Assert.Null(digest);
    }

    [Fact]
    public async Task GetMetadataDigestAsync_ReturnsNull_OnTimeoutLikeFailure()
    {
        using var httpClient = new HttpClient(new ThrowingHandler(new TaskCanceledException("timeout")));

        var source = new HttpAuthoritativeEvidenceDigestSource(
            httpClient,
            "https://meta.test/elections/{electionId}/digest",
            "https://audit.test/elections/{electionId}/digest");

        var digest = await source.GetMetadataDigestAsync("election-1", CancellationToken.None);

        Assert.Null(digest);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(_exception);
    }

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
