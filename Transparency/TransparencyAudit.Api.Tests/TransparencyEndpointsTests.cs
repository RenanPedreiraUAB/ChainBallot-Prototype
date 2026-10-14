using System.Net;
using System.Net.Http.Json;
using ChainBallot.PublicContracts;
using TransparencyAudit.Api.Domain;
using TransparencyAudit.Api.Endpoints;
using TransparencyAudit.Api.Services;

namespace TransparencyAudit.Api.Tests;

public sealed class TransparencyEndpointsTests
{
    [Fact]
    public async Task SnapshotEndpoint_ReturnsVerifiedSnapshot_WhenElectionProjectionExists()
    {
        using var factory = new TransparencyApiWebFactory(new[] { BuildFinalizedProjection("election-1") });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/transparency/elections/election-1/snapshot");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VerifiedResultSnapshot>();
        Assert.NotNull(body);
        Assert.Equal("election-1", body!.ElectionId);
        Assert.Equal("Finalized", body.LifecycleState);
    }

    [Fact]
    public async Task VerifyEndpoint_ReturnsVerified_WhenVidExistsInFinalizedElection()
    {
        using var factory = new TransparencyApiWebFactory(new[] { BuildFinalizedProjection("election-1") });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/transparency/elections/election-1/verify/vid-123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VidVerificationResponse>();
        Assert.NotNull(body);
        Assert.Equal(VerificationOutcome.Verified, body!.Outcome);
        Assert.NotNull(body.InclusionProofReference);
        Assert.NotNull(body.SettlementReference);
    }

    [Fact]
    public async Task VerifyEndpoint_ReturnsElectionNotFinalized_WhenLifecycleStateIsOpen()
    {
        using var factory = new TransparencyApiWebFactory(new[] { BuildOpenProjection("election-open") });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/transparency/elections/election-open/verify/vid-123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VidVerificationResponse>();
        Assert.NotNull(body);
        Assert.Equal(VerificationOutcome.ElectionNotFinalized, body!.Outcome);
        Assert.Null(body.InclusionProofReference);
    }

    [Fact]
    public async Task VerifyEndpoint_ReturnsNotFound_WhenVidDoesNotExist()
    {
        using var factory = new TransparencyApiWebFactory(new[] { BuildFinalizedProjection("election-1") });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/transparency/elections/election-1/verify/missing-vid");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VidVerificationResponse>();
        Assert.NotNull(body);
        Assert.Equal(VerificationOutcome.NotFound, body!.Outcome);
    }

    [Fact]
    public async Task VerifyEndpoint_ReturnsInconsistent_WhenProjectionIsStale()
    {
        var staleProjection = BuildFinalizedProjection("election-stale") with
        {
            IsStale = true,
            StaleReason = "metadata digest mismatch"
        };
        using var factory = new TransparencyApiWebFactory(new[] { staleProjection });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/transparency/elections/election-stale/verify/vid-123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VidVerificationResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(VerificationOutcome.Verified, body!.Outcome);
        Assert.Equal(VerificationOutcome.Inconsistent, body.Outcome);
        Assert.Contains("stale", body.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyEndpoint_ReturnsInconsistent_WhenCanonicalStateMismatchesProjection()
    {
        using var factory = new TransparencyApiWebFactory(
            new[] { BuildFinalizedProjection("election-canon-mismatch") },
            canonicalFinalizedStates: new Dictionary<string, bool> { ["election-canon-mismatch"] = false });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/transparency/elections/election-canon-mismatch/verify/vid-123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VidVerificationResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(VerificationOutcome.Verified, body!.Outcome);
        Assert.Equal(VerificationOutcome.Inconsistent, body.Outcome);
    }

    [Fact]
    public async Task VerifyEndpoint_ReturnsVerified_WhenCanonicalStateAlignsWithProjection()
    {
        using var factory = new TransparencyApiWebFactory(
            new[] { BuildFinalizedProjection("election-canon-aligned") },
            canonicalFinalizedStates: new Dictionary<string, bool> { ["election-canon-aligned"] = true });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/transparency/elections/election-canon-aligned/verify/vid-123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<VidVerificationResponse>();
        Assert.NotNull(body);
        Assert.Equal(VerificationOutcome.Verified, body!.Outcome);
    }

    [Fact]
    public async Task SnapshotEndpoint_ReturnsNotFound_WhenProjectionIsStale()
    {
        var staleProjection = BuildFinalizedProjection("election-snapshot-stale") with
        {
            IsStale = true,
            StaleReason = "audit digest mismatch"
        };
        using var factory = new TransparencyApiWebFactory(new[] { staleProjection });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/transparency/elections/election-snapshot-stale/snapshot");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task IngestEndpoint_RegistersProjectionAndVerificationTrail()
    {
        using var factory = new TransparencyApiWebFactory(Array.Empty<ElectionProjection>());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TransparencyApiWebFactory.AuthHeaderName, TransparencyApiWebFactory.IngestionToken);

        var request = new ProjectionIngestionRequest(
            "Finalized",
            "digest-ingested",
            BuildSettlementReference("election-ingest"),
            new Dictionary<string, long> { ["cand-a"] = 5 },
            new[]
            {
                new VidEvidenceRequest(
                    "vid-ingest",
                    "batch-z",
                    "commitment-z",
                    new InclusionProofReference("batch-z", "root-z", "commitment-z", 3, "proof://z", "hash-z"),
                    DateTimeOffset.UtcNow,
                    null)
            },
            "meta-digest-1",
            "audit-digest-1",
            false,
            null);

        var ingestResponse = await client.PostAsJsonAsync("/api/transparency/elections/election-ingest/ingest", request);

        Assert.Equal(HttpStatusCode.OK, ingestResponse.StatusCode);
        var status = await ingestResponse.Content.ReadFromJsonAsync<ProjectionStatusResponse>();
        Assert.NotNull(status);
        Assert.False(status!.IsStale);
        Assert.False(string.IsNullOrWhiteSpace(status.ConsistencyDigest));

        var verifyResponse = await client.GetAsync("/api/transparency/elections/election-ingest/verify/vid-ingest");
        var verifyBody = await verifyResponse.Content.ReadFromJsonAsync<VidVerificationResponse>();
        Assert.NotNull(verifyBody);
        Assert.Equal(VerificationOutcome.Verified, verifyBody!.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(verifyBody.ConsistencyDigest));
    }

    [Fact]
    public async Task MarkStaleAndReindexEndpoints_TransitionProjectionState()
    {
        using var factory = new TransparencyApiWebFactory(new[] { BuildFinalizedProjection("election-reindex") });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TransparencyApiWebFactory.AuthHeaderName, TransparencyApiWebFactory.IngestionToken);

        var staleResponse = await client.PostAsJsonAsync(
            "/api/transparency/elections/election-reindex/mark-stale",
            new StaleProjectionRequest("digest mismatch"));

        Assert.Equal(HttpStatusCode.OK, staleResponse.StatusCode);
        var staleStatus = await staleResponse.Content.ReadFromJsonAsync<ProjectionStatusResponse>();
        Assert.NotNull(staleStatus);
        Assert.True(staleStatus!.IsStale);
        Assert.Equal("digest mismatch", staleStatus.StaleReason);

        var reindexResponse = await client.PostAsync("/api/transparency/elections/election-reindex/reindex", content: null);

        Assert.Equal(HttpStatusCode.OK, reindexResponse.StatusCode);
        var reindexedStatus = await reindexResponse.Content.ReadFromJsonAsync<ProjectionStatusResponse>();
        Assert.NotNull(reindexedStatus);
        Assert.False(reindexedStatus!.IsStale);
        Assert.Null(reindexedStatus.StaleReason);
        Assert.False(string.IsNullOrWhiteSpace(reindexedStatus.ConsistencyDigest));
    }

    [Fact]
    public async Task IngestEndpoint_MarksProjectionStale_WhenAuthoritativeDigestsMismatch()
    {
        using var factory = new TransparencyApiWebFactory(
            Array.Empty<ElectionProjection>(),
            new Dictionary<string, string> { ["election-mismatch"] = "meta-authoritative" },
            new Dictionary<string, string> { ["election-mismatch"] = "audit-authoritative" });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TransparencyApiWebFactory.AuthHeaderName, TransparencyApiWebFactory.IngestionToken);

        var request = new ProjectionIngestionRequest(
            "Finalized",
            "digest-mismatch",
            BuildSettlementReference("election-mismatch"),
            new Dictionary<string, long> { ["cand-a"] = 2 },
            new[]
            {
                new VidEvidenceRequest(
                    "vid-mismatch",
                    "batch-m",
                    "commitment-m",
                    new InclusionProofReference("batch-m", "root-m", "commitment-m", 1, null, null),
                    DateTimeOffset.UtcNow,
                    null)
            },
            "meta-provided",
            "audit-provided",
            false,
            null);

        var ingestResponse = await client.PostAsJsonAsync("/api/transparency/elections/election-mismatch/ingest", request);

        Assert.Equal(HttpStatusCode.OK, ingestResponse.StatusCode);
        var status = await ingestResponse.Content.ReadFromJsonAsync<ProjectionStatusResponse>();
        Assert.NotNull(status);
        Assert.True(status!.IsStale);
        Assert.Contains("metadata digest mismatch", status.StaleReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("audit digest mismatch", status.StaleReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("meta-authoritative", status.MetadataDigest);
        Assert.Equal("audit-authoritative", status.AuditDigest);
    }

    [Fact]
    public async Task ReindexEndpoint_KeepsProjectionStale_WhenAuthoritativeDigestsMismatch()
    {
        using var factory = new TransparencyApiWebFactory(
            new[] { BuildFinalizedProjection("election-reindex-mismatch") },
            new Dictionary<string, string> { ["election-reindex-mismatch"] = "meta-authoritative" },
            new Dictionary<string, string> { ["election-reindex-mismatch"] = "audit-authoritative" });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TransparencyApiWebFactory.AuthHeaderName, TransparencyApiWebFactory.IngestionToken);

        var reindexResponse = await client.PostAsync("/api/transparency/elections/election-reindex-mismatch/reindex", content: null);

        Assert.Equal(HttpStatusCode.OK, reindexResponse.StatusCode);
        var status = await reindexResponse.Content.ReadFromJsonAsync<ProjectionStatusResponse>();
        Assert.NotNull(status);
        Assert.True(status!.IsStale);
        Assert.Contains("metadata", status.StaleReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("audit", status.StaleReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("meta-authoritative", status.MetadataDigest);
        Assert.Equal("audit-authoritative", status.AuditDigest);
    }

    [Fact]
    public async Task CanonicalStateEndpoint_ReturnsMockState_WhenCanonicalAdapterConfigured()
    {
        using var factory = new TransparencyApiWebFactory(
            new[] { BuildFinalizedProjection("election-chain") },
            canonicalFinalizedStates: new Dictionary<string, bool> { ["election-chain"] = true });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/transparency/elections/election-chain/canonical-state");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CanonicalElectionState>();
        Assert.NotNull(body);
        Assert.True(body!.IsFinalized);
        Assert.Equal("MockSolana", body.Source);
    }

    [Fact]
    public async Task IngestEndpoint_WithoutCredentials_IsRejected()
    {
        using var factory = new TransparencyApiWebFactory(Array.Empty<ElectionProjection>());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/transparency/elections/election-noauth/ingest",
            new ProjectionIngestionRequest("Finalized", "d", BuildSettlementReference("election-noauth"), new Dictionary<string, long>(), Array.Empty<VidEvidenceRequest>(), null, null, false, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IngestEndpoint_WithReconciliationRole_IsRejected()
    {
        using var factory = new TransparencyApiWebFactory(Array.Empty<ElectionProjection>());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TransparencyApiWebFactory.AuthHeaderName, TransparencyApiWebFactory.ReconciliationToken);

        var response = await client.PostAsJsonAsync(
            "/api/transparency/elections/election-wrongrole/ingest",
            new ProjectionIngestionRequest("Finalized", "d", BuildSettlementReference("election-wrongrole"), new Dictionary<string, long>(), Array.Empty<VidEvidenceRequest>(), null, null, false, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MarkStaleEndpoint_WithoutCredentials_IsRejected()
    {
        using var factory = new TransparencyApiWebFactory(new[] { BuildFinalizedProjection("election-noauth-stale") });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/transparency/elections/election-noauth-stale/mark-stale",
            new StaleProjectionRequest("reason"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ResyncEndpoint_WithoutCredentials_IsRejected()
    {
        using var factory = new TransparencyApiWebFactory(Array.Empty<ElectionProjection>());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/transparency/events/resync",
            new CanonicalResyncTriggerRequest("election-noauth-resync", "reason", "source", "correlation-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReconciliationDispute_WithoutCredentials_IsRejected()
    {
        using var factory = new TransparencyApiWebFactory(new[] { BuildFinalizedProjection("election-noauth-recon") });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/transparency/reconciliation/disputes",
            new ReconciliationDisputeRequest("election-noauth-recon", "reason", "actor", null, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReconciliationDispute_WithIngestionRole_IsRejected()
    {
        using var factory = new TransparencyApiWebFactory(new[] { BuildFinalizedProjection("election-wrongrole-recon") });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TransparencyApiWebFactory.AuthHeaderName, TransparencyApiWebFactory.IngestionToken);

        var response = await client.PostAsJsonAsync(
            "/api/transparency/reconciliation/disputes",
            new ReconciliationDisputeRequest("election-wrongrole-recon", "reason", "actor", null, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReconciliationWorkflow_StoresImmutableCaseEvents()
    {
        using var factory = new TransparencyApiWebFactory(new[] { BuildFinalizedProjection("election-recon") });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TransparencyApiWebFactory.AuthHeaderName, TransparencyApiWebFactory.ReconciliationToken);

        var disputeResponse = await client.PostAsJsonAsync(
            "/api/transparency/reconciliation/disputes",
            new ReconciliationDisputeRequest("election-recon", "digest mismatch", "auditor-1", "external-1", "opened by watcher"));
        Assert.Equal(HttpStatusCode.OK, disputeResponse.StatusCode);

        var created = await disputeResponse.Content.ReadFromJsonAsync<ReconciliationCaseResponse>();
        Assert.NotNull(created);
        Assert.Equal("Disputed", created!.Status);

        var overrideResponse = await client.PostAsJsonAsync(
            $"/api/transparency/reconciliation/{created.CaseId}/override",
            new ReconciliationOverrideRequest("operator-1", "OverrideAccepted", "evidence reviewed", "external-2", "manual override"));
        Assert.Equal(HttpStatusCode.OK, overrideResponse.StatusCode);

        var overridden = await overrideResponse.Content.ReadFromJsonAsync<ReconciliationCaseResponse>();
        Assert.NotNull(overridden);
        Assert.Equal("Overridden", overridden!.Status);

        var resolveResponse = await client.PostAsJsonAsync(
            $"/api/transparency/reconciliation/{created.CaseId}/resolve",
            new ReconciliationResolveRequest("operator-2", "Confirmed", "resolved after reindex", "closing case"));
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);

        var resolved = await resolveResponse.Content.ReadFromJsonAsync<ReconciliationCaseResponse>();
        Assert.NotNull(resolved);
        Assert.Equal("ResolvedConfirmed", resolved!.Status);
        Assert.Equal(3, resolved.Events.Count);
        Assert.Equal("DisputeOpened", resolved.Events[0].EventType);
        Assert.Equal("OverrideRequested", resolved.Events[1].EventType);
        Assert.Equal("Resolved", resolved.Events[2].EventType);

        var listResponse = await client.GetAsync("/api/transparency/elections/election-recon/reconciliation/cases");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var items = await listResponse.Content.ReadFromJsonAsync<ReconciliationCaseResponse[]>();
        Assert.NotNull(items);
        Assert.Contains(items!, x => x.CaseId == created.CaseId);
    }

    private static ElectionProjection BuildFinalizedProjection(string electionId)
    {
        var settlement = BuildSettlementReference(electionId);
        var inclusion = new InclusionProofReference(
            "batch-1",
            "root-1",
            "commitment-1",
            0,
            "proof://blob/1",
            "proof-hash-1");

        return new ElectionProjection(
            electionId,
            "Finalized",
            settlement,
            new Dictionary<string, long> { ["cand-a"] = 10, ["cand-b"] = 8 },
            "digest-1",
            DateTimeOffset.UtcNow,
            new[]
            {
                new VidEntry(
                    electionId,
                    "vid-123",
                    "batch-1",
                    "commitment-1",
                    settlement,
                    inclusion,
                    DateTimeOffset.UtcNow,
                    "consistency-1")
            });
    }

    private static ElectionProjection BuildOpenProjection(string electionId)
    {
        return new ElectionProjection(
            electionId,
            "Open",
            BuildSettlementReference(electionId),
            new Dictionary<string, long>(),
            "digest-open",
            DateTimeOffset.UtcNow,
            Array.Empty<VidEntry>());
    }

    private static SettlementReference BuildSettlementReference(string electionId)
        => new(
            "Localnet",
            "program-1",
            electionId,
            "tx-1",
            100UL,
            DateTimeOffset.UtcNow,
            "finalized");
}
