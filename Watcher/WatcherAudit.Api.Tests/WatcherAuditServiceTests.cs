using ChainBallot.PublicContracts;
using Microsoft.Extensions.Logging.Abstractions;
using WatcherAudit.Api.Domain;
using WatcherAudit.Api.Services;

namespace WatcherAudit.Api.Tests;

public sealed class WatcherAuditServiceTests
{
    [Fact]
    public async Task RunAuditAsync_ReturnsConsistent_WhenAllChecksPass()
    {
        var service = BuildService(
            snapshotResult: new UpstreamReadResult<VerifiedResultSnapshot>(
                UpstreamReadStatus.Success,
                new VerifiedResultSnapshot(
                    "election-1",
                    "result-digest-1",
                    "Finalized",
                    new SettlementReference("Localnet", "program-1", "election-1", "tx-1", 7, DateTimeOffset.UtcNow, "finalized"),
                    new Dictionary<string, long> { ["cand-a"] = 10 },
                    DateTimeOffset.UtcNow),
                null),
            projectionResult: new UpstreamReadResult<ProjectionStatusDto>(
                UpstreamReadStatus.Success,
                new ProjectionStatusDto("election-1", "Finalized", false, null, "consistency-1", "meta-1", "audit-1", 1, DateTimeOffset.UtcNow),
                null),
            canonicalResult: new UpstreamReadResult<CanonicalElectionStateDto>(
                UpstreamReadStatus.Success,
                new CanonicalElectionStateDto("election-1", true, "Mock", DateTimeOffset.UtcNow, null),
                null),
            digests: new AuthoritativeDigestPair("meta-1", "audit-1"));

        var report = await service.RunAuditAsync("election-1", "tester", CancellationToken.None);

        Assert.Equal(WatcherReportStatus.Consistent, report.Status);
        Assert.NotEmpty(report.Signature);
        Assert.NotEmpty(report.ReportDigest);
        Assert.All(report.Checks, check => Assert.True(check.Passed));
    }

    [Fact]
    public async Task RunAuditAsync_ReturnsInconsistent_WhenMetadataDigestMismatches()
    {
        var service = BuildService(
            snapshotResult: new UpstreamReadResult<VerifiedResultSnapshot>(
                UpstreamReadStatus.Success,
                new VerifiedResultSnapshot(
                    "election-1",
                    "result-digest-1",
                    "Finalized",
                    new SettlementReference("Localnet", "program-1", "election-1", "tx-1", 7, DateTimeOffset.UtcNow, "finalized"),
                    new Dictionary<string, long> { ["cand-a"] = 10 },
                    DateTimeOffset.UtcNow),
                null),
            projectionResult: new UpstreamReadResult<ProjectionStatusDto>(
                UpstreamReadStatus.Success,
                new ProjectionStatusDto("election-1", "Finalized", false, null, "consistency-1", "meta-from-projection", "audit-1", 1, DateTimeOffset.UtcNow),
                null),
            canonicalResult: new UpstreamReadResult<CanonicalElectionStateDto>(
                UpstreamReadStatus.Success,
                new CanonicalElectionStateDto("election-1", true, "Mock", DateTimeOffset.UtcNow, null),
                null),
            digests: new AuthoritativeDigestPair("meta-authoritative", "audit-1"));

        var report = await service.RunAuditAsync("election-1", "tester", CancellationToken.None);

        Assert.Equal(WatcherReportStatus.Inconsistent, report.Status);
        Assert.Contains(report.Checks, c => c.Name == "metadata-digest-match" && c.Passed == false);
    }

    private static WatcherAuditService BuildService(
        UpstreamReadResult<VerifiedResultSnapshot> snapshotResult,
        UpstreamReadResult<ProjectionStatusDto> projectionResult,
        UpstreamReadResult<CanonicalElectionStateDto> canonicalResult,
        AuthoritativeDigestPair digests)
    {
        return new WatcherAuditService(
            new StubTransparencyClient(snapshotResult, projectionResult, canonicalResult),
            new StubDigestSource(digests),
            new InMemoryWatcherReportRepository(),
            new Sha256WatcherDigestCalculator(),
            new StubReportSigner(),
            NullLogger<WatcherAuditService>.Instance);
    }

    private sealed class StubTransparencyClient : ITransparencyAuditReadClient
    {
        private readonly UpstreamReadResult<VerifiedResultSnapshot> _snapshot;
        private readonly UpstreamReadResult<ProjectionStatusDto> _projection;
        private readonly UpstreamReadResult<CanonicalElectionStateDto> _canonical;

        public StubTransparencyClient(
            UpstreamReadResult<VerifiedResultSnapshot> snapshot,
            UpstreamReadResult<ProjectionStatusDto> projection,
            UpstreamReadResult<CanonicalElectionStateDto> canonical)
        {
            _snapshot = snapshot;
            _projection = projection;
            _canonical = canonical;
        }

        public Task<UpstreamReadResult<VerifiedResultSnapshot>> GetSnapshotAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult(_snapshot);

        public Task<UpstreamReadResult<ProjectionStatusDto>> GetProjectionStatusAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult(_projection);

        public Task<UpstreamReadResult<CanonicalElectionStateDto>> GetCanonicalStateAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult(_canonical);
    }

    private sealed class StubDigestSource : IAuthoritativeDigestSource
    {
        private readonly AuthoritativeDigestPair _digests;

        public StubDigestSource(AuthoritativeDigestPair digests)
        {
            _digests = digests;
        }

        public Task<AuthoritativeDigestPair> GetDigestsAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult(_digests);
    }

    private sealed class StubReportSigner : IWatcherReportSigner
    {
        public string Sign(string digest)
            => $"sig-{digest}";
    }

    private sealed class InMemoryWatcherReportRepository : IWatcherReportRepository
    {
        private readonly List<WatcherAuditReport> _reports = [];

        public Task<WatcherAuditReport> SaveAsync(WatcherAuditReport report, CancellationToken cancellationToken)
        {
            _reports.Add(report);
            return Task.FromResult(report);
        }

        public Task<WatcherAuditReport?> GetByIdAsync(string reportId, CancellationToken cancellationToken)
            => Task.FromResult(_reports.FirstOrDefault(r => r.ReportId == reportId));

        public Task<WatcherAuditReport?> GetLatestByElectionAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult(_reports.Where(r => r.ElectionId == electionId).OrderByDescending(r => r.GeneratedAtUtc).FirstOrDefault());

        public Task<IReadOnlyList<WatcherAuditReport>> ListByElectionAsync(string electionId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<WatcherAuditReport>>(_reports.Where(r => r.ElectionId == electionId).ToArray());
    }
}
