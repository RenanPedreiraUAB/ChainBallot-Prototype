using System.Net;
using System.Net.Http.Json;
using ChainBallot.PublicContracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WatcherAudit.Api.Domain;
using WatcherAudit.Api.Services;

namespace WatcherAudit.Api.Tests;

public sealed class WatcherEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string OperatorHeaderName = "X-Operator-Key";
    private const string OperatorHeaderValue = "watcher-operator-key";

    private readonly WebApplicationFactory<Program> _factory;

    public WatcherEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITransparencyAuditReadClient>();
                services.RemoveAll<IAuthoritativeDigestSource>();
                services.RemoveAll<IWatcherReportRepository>();
                services.RemoveAll<IWatcherReportSigner>();
                services.RemoveAll<IWatcherDigestCalculator>();
                services.RemoveAll<IWatcherAuditService>();

                services.AddSingleton<ITransparencyAuditReadClient>(new StubTransparencyClient());
                services.AddSingleton<IAuthoritativeDigestSource>(new StubDigestSource());
                services.AddSingleton<IWatcherReportRepository, InMemoryWatcherReportRepository>();
                services.AddSingleton<IWatcherDigestCalculator, Sha256WatcherDigestCalculator>();
                services.AddSingleton<IWatcherReportSigner, StubReportSigner>();
                services.AddSingleton<IWatcherAuditService, WatcherAuditService>();
            });
        });
    }

    [Fact]
    public async Task RunAudit_ThenLatestReport_ReturnsOk()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(OperatorHeaderName, OperatorHeaderValue);

        var runResponse = await client.PostAsJsonAsync("/api/watcher/elections/election-1/run-audit", new { requestedBy = "operator" });
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);

        var latestResponse = await client.GetAsync("/api/watcher/elections/election-1/latest-report");
        Assert.Equal(HttpStatusCode.OK, latestResponse.StatusCode);

        var latestContent = await latestResponse.Content.ReadAsStringAsync();
        Assert.Contains("election-1", latestContent, StringComparison.Ordinal);
        Assert.Contains("Consistent", latestContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EvidenceExport_ReturnsSignedReportPayload()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(OperatorHeaderName, OperatorHeaderValue);

        var runResponse = await client.PostAsJsonAsync("/api/watcher/elections/election-2/run-audit", new { requestedBy = "operator" });
        var runBody = await runResponse.Content.ReadAsStringAsync();
        var reportId = ExtractJsonString(runBody, "reportId");

        Assert.False(string.IsNullOrWhiteSpace(reportId));

        var evidenceResponse = await client.GetAsync($"/api/watcher/reports/{reportId}/evidence");
        Assert.Equal(HttpStatusCode.OK, evidenceResponse.StatusCode);

        var evidenceBody = await evidenceResponse.Content.ReadAsStringAsync();
        Assert.Contains("signature", evidenceBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reportDigest", evidenceBody, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractJsonString(string json, string property)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.GetString();
    }

    private sealed class StubTransparencyClient : ITransparencyAuditReadClient
    {
        public Task<UpstreamReadResult<VerifiedResultSnapshot>> GetSnapshotAsync(string electionId, CancellationToken cancellationToken)
        {
            var snapshot = new VerifiedResultSnapshot(
                electionId,
                "result-digest",
                "Finalized",
                new SettlementReference("Localnet", "program-1", electionId, "tx-1", 12, DateTimeOffset.UtcNow, "finalized"),
                new Dictionary<string, long> { ["cand-a"] = 17 },
                DateTimeOffset.UtcNow);
            return Task.FromResult(new UpstreamReadResult<VerifiedResultSnapshot>(UpstreamReadStatus.Success, snapshot, null));
        }

        public Task<UpstreamReadResult<ProjectionStatusDto>> GetProjectionStatusAsync(string electionId, CancellationToken cancellationToken)
        {
            var projection = new ProjectionStatusDto(electionId, "Finalized", false, null, "consistency-1", "meta-1", "audit-1", 1, DateTimeOffset.UtcNow);
            return Task.FromResult(new UpstreamReadResult<ProjectionStatusDto>(UpstreamReadStatus.Success, projection, null));
        }

        public Task<UpstreamReadResult<CanonicalElectionStateDto>> GetCanonicalStateAsync(string electionId, CancellationToken cancellationToken)
        {
            var canonical = new CanonicalElectionStateDto(electionId, true, "Mock", DateTimeOffset.UtcNow, null);
            return Task.FromResult(new UpstreamReadResult<CanonicalElectionStateDto>(UpstreamReadStatus.Success, canonical, null));
        }
    }

    private sealed class StubDigestSource : IAuthoritativeDigestSource
    {
        public Task<AuthoritativeDigestPair> GetDigestsAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult(new AuthoritativeDigestPair("meta-1", "audit-1"));
    }

    private sealed class StubReportSigner : IWatcherReportSigner
    {
        public string Sign(string digest) => $"sig-{digest}";
    }

    private sealed class InMemoryWatcherReportRepository : IWatcherReportRepository
    {
        private readonly List<WatcherAuditReport> _items = [];

        public Task<WatcherAuditReport> SaveAsync(WatcherAuditReport report, CancellationToken cancellationToken)
        {
            _items.Add(report);
            return Task.FromResult(report);
        }

        public Task<WatcherAuditReport?> GetByIdAsync(string reportId, CancellationToken cancellationToken)
            => Task.FromResult(_items.FirstOrDefault(x => string.Equals(x.ReportId, reportId, StringComparison.OrdinalIgnoreCase)));

        public Task<WatcherAuditReport?> GetLatestByElectionAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult(_items.Where(x => string.Equals(x.ElectionId, electionId, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.GeneratedAtUtc).FirstOrDefault());

        public Task<IReadOnlyList<WatcherAuditReport>> ListByElectionAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<WatcherAuditReport>>(_items.Where(x => string.Equals(x.ElectionId, electionId, StringComparison.OrdinalIgnoreCase)).ToArray());
    }
}
