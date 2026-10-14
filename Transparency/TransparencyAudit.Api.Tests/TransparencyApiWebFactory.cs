using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TransparencyAudit.Api.Domain;
using TransparencyAudit.Api.Services;

namespace TransparencyAudit.Api.Tests;

public sealed class TransparencyApiWebFactory : WebApplicationFactory<Program>
{
    public const string AuthHeaderName = "X-Transparency-Auth";
    public const string IngestionToken = "test-ingestion-token";
    public const string ReconciliationToken = "test-reconciliation-token";

    private readonly IReadOnlyList<ElectionProjection> _projections;
    private readonly IReadOnlyDictionary<string, string>? _authoritativeMetadataDigests;
    private readonly IReadOnlyDictionary<string, string>? _authoritativeAuditDigests;
    private readonly IReadOnlyDictionary<string, bool>? _canonicalFinalizedStates;

    public TransparencyApiWebFactory(
        IReadOnlyList<ElectionProjection> projections,
        IReadOnlyDictionary<string, string>? authoritativeMetadataDigests = null,
        IReadOnlyDictionary<string, string>? authoritativeAuditDigests = null,
        IReadOnlyDictionary<string, bool>? canonicalFinalizedStates = null)
    {
        _projections = projections;
        _authoritativeMetadataDigests = authoritativeMetadataDigests;
        _authoritativeAuditDigests = authoritativeAuditDigests;
        _canonicalFinalizedStates = canonicalFinalizedStates;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Transparency:Authentication:HeaderName"] = AuthHeaderName,
                ["Transparency:Authentication:Credentials:0:Token"] = IngestionToken,
                ["Transparency:Authentication:Credentials:0:Name"] = "test-ingestion",
                ["Transparency:Authentication:Credentials:0:Roles:0"] = "transparency.ingestion",
                ["Transparency:Authentication:Credentials:1:Token"] = ReconciliationToken,
                ["Transparency:Authentication:Credentials:1:Name"] = "test-reconciliation",
                ["Transparency:Authentication:Credentials:1:Roles:0"] = "transparency.reconciliation"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IProjectionRepository>();
            services.AddSingleton<IProjectionRepository>(new InMemoryProjectionRepository(_projections));
            services.RemoveAll<IAuthoritativeEvidenceDigestSource>();
            services.AddSingleton<IAuthoritativeEvidenceDigestSource>(
                new InMemoryAuthoritativeEvidenceDigestSource(_authoritativeMetadataDigests, _authoritativeAuditDigests));
            services.RemoveAll<ISolanaSettlementReadAdapter>();
            services.AddSingleton<ISolanaSettlementReadAdapter>(
                _canonicalFinalizedStates is null
                    ? new ProjectionOnlySolanaSettlementReadAdapter()
                    : new MockSolanaSettlementReadAdapter(_canonicalFinalizedStates));
        });
    }

    private sealed class InMemoryProjectionRepository : IProjectionRepository
    {
        private readonly List<ElectionProjection> _projections;
        private readonly IConsistencyDigestCalculator _digestCalculator = new Sha256ConsistencyDigestCalculator();

        public InMemoryProjectionRepository(IReadOnlyList<ElectionProjection> projections)
        {
            _projections = projections.ToList();
        }

        public Task<ElectionProjection?> GetElectionProjectionAsync(string electionId, CancellationToken cancellationToken)
        {
            var projection = _projections.FirstOrDefault(p => string.Equals(p.ElectionId, electionId, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(projection);
        }

        public Task<VidEntry?> GetVidEntryAsync(string electionId, string vid, CancellationToken cancellationToken)
        {
            var projection = _projections.FirstOrDefault(p => string.Equals(p.ElectionId, electionId, StringComparison.OrdinalIgnoreCase));
            var entry = projection?.VidEntries.FirstOrDefault(v => string.Equals(v.Vid, vid, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(entry);
        }

        public Task<ElectionProjection> UpsertProjectionEvidenceAsync(ProjectionEvidenceIngestion evidence, CancellationToken cancellationToken)
        {
            var vidEntries = evidence.VidEntries
                .Select(v =>
                {
                    var vidDigest = string.IsNullOrWhiteSpace(v.ConsistencyDigest)
                        ? _digestCalculator.ComputeVidDigest(
                            evidence.ElectionId,
                            v.Vid,
                            v.BatchId,
                            v.BallotCommitment,
                            v.InclusionProofReference.MerkleRoot,
                            v.InclusionProofReference.LeafIndex)
                        : v.ConsistencyDigest;

                    return new VidEntry(
                        evidence.ElectionId,
                        v.Vid,
                        v.BatchId,
                        v.BallotCommitment,
                        evidence.SettlementReference,
                        v.InclusionProofReference,
                        v.RecordedAtUtc,
                        vidDigest);
                })
                .ToArray();

            var digestInput = vidEntries
                .Select(v => new VidEvidenceEntry(
                    v.Vid,
                    v.BatchId,
                    v.BallotCommitment,
                    v.InclusionProofReference,
                    v.RecordedAtUtc,
                    v.ConsistencyDigest))
                .ToArray();

            var digest = _digestCalculator.ComputeElectionDigest(
                evidence.ElectionId,
                evidence.LifecycleState,
                evidence.ResultDigest,
                evidence.CandidateTallies,
                digestInput,
                evidence.MetadataDigest,
                evidence.AuditDigest);

            var projection = new ElectionProjection(
                evidence.ElectionId,
                evidence.LifecycleState,
                evidence.SettlementReference,
                evidence.CandidateTallies,
                evidence.ResultDigest,
                DateTimeOffset.UtcNow,
                vidEntries,
                digest,
                evidence.MetadataDigest,
                evidence.AuditDigest,
                evidence.IsStale,
                evidence.StaleReason);

            var index = _projections.FindIndex(p => string.Equals(p.ElectionId, evidence.ElectionId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _projections[index] = projection;
            }
            else
            {
                _projections.Add(projection);
            }

            return Task.FromResult(projection);
        }

        public Task<ElectionProjection?> MarkElectionStaleAsync(string electionId, string staleReason, CancellationToken cancellationToken)
        {
            var index = _projections.FindIndex(p => string.Equals(p.ElectionId, electionId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return Task.FromResult<ElectionProjection?>(null);
            }

            var updated = _projections[index] with
            {
                IsStale = true,
                StaleReason = staleReason,
                GeneratedAtUtc = DateTimeOffset.UtcNow
            };

            _projections[index] = updated;
            return Task.FromResult<ElectionProjection?>(updated);
        }

        public Task<ElectionProjection?> ReindexElectionAsync(string electionId, CancellationToken cancellationToken)
        {
            var index = _projections.FindIndex(p => string.Equals(p.ElectionId, electionId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return Task.FromResult<ElectionProjection?>(null);
            }

            var source = _projections[index];
            var vidEntries = source.VidEntries
                .Select(v =>
                {
                    var digest = string.IsNullOrWhiteSpace(v.ConsistencyDigest)
                        ? _digestCalculator.ComputeVidDigest(source.ElectionId, v.Vid, v.BatchId, v.BallotCommitment, v.InclusionProofReference.MerkleRoot, v.InclusionProofReference.LeafIndex)
                        : v.ConsistencyDigest;
                    return v with { ConsistencyDigest = digest };
                })
                .ToArray();

            var digestInput = vidEntries
                .Select(v => new VidEvidenceEntry(v.Vid, v.BatchId, v.BallotCommitment, v.InclusionProofReference, v.RecordedAtUtc, v.ConsistencyDigest))
                .ToArray();

            var updated = source with
            {
                VidEntries = vidEntries,
                ConsistencyDigest = _digestCalculator.ComputeElectionDigest(
                    source.ElectionId,
                    source.LifecycleState,
                    source.ResultDigest,
                    source.CandidateTallies,
                    digestInput,
                    source.MetadataDigest,
                    source.AuditDigest),
                IsStale = false,
                StaleReason = null,
                GeneratedAtUtc = DateTimeOffset.UtcNow
            };

            _projections[index] = updated;
            return Task.FromResult<ElectionProjection?>(updated);
        }
    }
}
