using TransparencyAudit.Api.Domain;

namespace TransparencyAudit.Api.Services;

public sealed class ProjectionManagementService : IProjectionManagementService
{
    private readonly IProjectionRepository _projectionRepository;
    private readonly IMetadataCorrelationService _metadataCorrelationService;
    private readonly IAuditCorrelationService _auditCorrelationService;

    public ProjectionManagementService(
        IProjectionRepository projectionRepository,
        IMetadataCorrelationService metadataCorrelationService,
        IAuditCorrelationService auditCorrelationService)
    {
        _projectionRepository = projectionRepository;
        _metadataCorrelationService = metadataCorrelationService;
        _auditCorrelationService = auditCorrelationService;
    }

    public async Task<ElectionProjection> IngestEvidenceAsync(ProjectionEvidenceIngestion evidence, CancellationToken cancellationToken)
    {
        var metadataCorrelation = await _metadataCorrelationService.CorrelateAsync(evidence.ElectionId, evidence.MetadataDigest, cancellationToken).ConfigureAwait(false);
        var auditCorrelation = await _auditCorrelationService.CorrelateAsync(evidence.ElectionId, evidence.AuditDigest, cancellationToken).ConfigureAwait(false);

        var mismatchReasons = new[] { metadataCorrelation, auditCorrelation }
            .Where(x => !x.IsConsistent && !string.IsNullOrWhiteSpace(x.Reason))
            .Select(x => x.Reason!)
            .ToArray();

        var isStale = evidence.IsStale || mismatchReasons.Length > 0;
        var staleReason = mismatchReasons.Length > 0
            ? string.Join("; ", mismatchReasons)
            : evidence.StaleReason;

        var correlatedEvidence = evidence with
        {
            MetadataDigest = metadataCorrelation.EffectiveDigest,
            AuditDigest = auditCorrelation.EffectiveDigest,
            IsStale = isStale,
            StaleReason = staleReason
        };

        return await _projectionRepository.UpsertProjectionEvidenceAsync(correlatedEvidence, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ElectionProjection?> ReindexElectionAsync(string electionId, CancellationToken cancellationToken)
    {
        var existing = await _projectionRepository.GetElectionProjectionAsync(electionId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        var metadataCorrelation = await _metadataCorrelationService.CorrelateAsync(electionId, existing.MetadataDigest, cancellationToken).ConfigureAwait(false);
        var auditCorrelation = await _auditCorrelationService.CorrelateAsync(electionId, existing.AuditDigest, cancellationToken).ConfigureAwait(false);

        var mismatchReasons = new[] { metadataCorrelation, auditCorrelation }
            .Where(x => !x.IsConsistent && !string.IsNullOrWhiteSpace(x.Reason))
            .Select(x => x.Reason!)
            .ToArray();

        await _projectionRepository.UpsertProjectionEvidenceAsync(
            new ProjectionEvidenceIngestion(
                existing.ElectionId,
                existing.LifecycleState,
                existing.ResultDigest,
                existing.SettlementReference,
                existing.CandidateTallies,
                existing.VidEntries
                    .Select(v => new VidEvidenceEntry(v.Vid, v.BatchId, v.BallotCommitment, v.InclusionProofReference, v.RecordedAtUtc, v.ConsistencyDigest))
                    .ToArray(),
                metadataCorrelation.EffectiveDigest,
                auditCorrelation.EffectiveDigest,
                existing.IsStale || mismatchReasons.Length > 0,
                mismatchReasons.Length > 0 ? string.Join("; ", mismatchReasons) : existing.StaleReason),
            cancellationToken).ConfigureAwait(false);

        var reindexed = await _projectionRepository.ReindexElectionAsync(electionId, cancellationToken).ConfigureAwait(false);
        if (reindexed is null)
        {
            return null;
        }

        if (mismatchReasons.Length > 0)
        {
            return await _projectionRepository.MarkElectionStaleAsync(electionId, string.Join("; ", mismatchReasons), cancellationToken).ConfigureAwait(false);
        }

        return reindexed;
    }

    public Task<ElectionProjection?> MarkStaleAsync(string electionId, string staleReason, CancellationToken cancellationToken)
        => _projectionRepository.MarkElectionStaleAsync(electionId, staleReason, cancellationToken);
}
