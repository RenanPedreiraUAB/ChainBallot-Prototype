using System.Net.Http.Json;

namespace ElectionProcessing.Api.Processing;

public sealed class HttpProjectionEvidencePublisher(HttpClient httpClient) : IProjectionEvidencePublisher
{
    public async Task PublishBatchAnchoredAsync(ProjectionEvidenceBatch batch, CancellationToken cancellationToken)
    {
        var request = new ProjectionIngestionRequest(
            LifecycleState: batch.LifecycleState,
            ResultDigest: batch.ResultDigest,
            SettlementReference: batch.SettlementReference,
            CandidateTallies: batch.CandidateTallies,
            VidEntries: batch.VidEntries
                .Select(v => new ProjectionVidEvidenceRequest(
                    Vid: v.Vid,
                    BatchId: v.BatchId,
                    BallotCommitment: v.BallotCommitment,
                    InclusionProofReference: v.InclusionProofReference,
                    RecordedAtUtc: v.RecordedAtUtc,
                    ConsistencyDigest: v.ConsistencyDigest))
                .ToArray(),
            MetadataDigest: batch.MetadataDigest,
            AuditDigest: batch.AuditDigest,
            IsStale: batch.IsStale,
            StaleReason: batch.StaleReason);

        using var response = await httpClient.PostAsJsonAsync(
            $"/api/transparency/elections/{Uri.EscapeDataString(batch.ElectionId)}/ingest",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private sealed record ProjectionIngestionRequest(
        string LifecycleState,
        string ResultDigest,
        ChainBallot.PublicContracts.SettlementReference SettlementReference,
        IReadOnlyDictionary<string, long> CandidateTallies,
        IReadOnlyList<ProjectionVidEvidenceRequest> VidEntries,
        string? MetadataDigest,
        string? AuditDigest,
        bool IsStale,
        string? StaleReason);

    private sealed record ProjectionVidEvidenceRequest(
        string Vid,
        string BatchId,
        string BallotCommitment,
        ChainBallot.PublicContracts.InclusionProofReference InclusionProofReference,
        DateTimeOffset RecordedAtUtc,
        string? ConsistencyDigest);
}
