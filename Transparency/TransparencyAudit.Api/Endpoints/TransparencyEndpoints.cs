using Microsoft.AspNetCore.Http.HttpResults;
using ChainBallot.PublicContracts;
using TransparencyAudit.Api.Application;
using TransparencyAudit.Api.Domain;
using TransparencyAudit.Api.Services;

namespace TransparencyAudit.Api.Endpoints;

public static class TransparencyEndpoints
{
    public static IEndpointRouteBuilder MapTransparencyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/transparency").WithTags("Transparency");

        group.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        group.MapGet(
                "/elections/{electionId}/snapshot",
                async Task<Results<Ok<VerifiedResultSnapshot>, NotFound>> (
                    string electionId,
                    IResultSnapshotService snapshotService,
                    CancellationToken cancellationToken) =>
                {
                    var snapshot = await snapshotService.GetSnapshotAsync(electionId, cancellationToken).ConfigureAwait(false);
                    return snapshot is null ? TypedResults.NotFound() : TypedResults.Ok(snapshot);
                })
            .WithName("GetElectionSnapshot")
            .Produces<VerifiedResultSnapshot>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet(
                "/elections/{electionId}/verify/{vid}",
                async Task<Ok<VidVerificationResponse>> (
                    string electionId,
                    string vid,
                    IVerificationService verificationService,
                    CancellationToken cancellationToken) =>
                {
                    var response = await verificationService.VerifyVidAsync(electionId, vid, cancellationToken).ConfigureAwait(false);
                    return TypedResults.Ok(response);
                })
            .WithName("VerifyVid")
            .Produces<VidVerificationResponse>(StatusCodes.Status200OK);

        group.MapGet(
                "/elections/{electionId}/projection-status",
                async Task<Results<Ok<ProjectionStatusResponse>, NotFound>>(
                    string electionId,
                    IProjectionRepository projectionRepository,
                    CancellationToken cancellationToken) =>
                {
                    var projection = await projectionRepository.GetElectionProjectionAsync(electionId, cancellationToken).ConfigureAwait(false);
                    if (projection is null)
                    {
                        return TypedResults.NotFound();
                    }

                    return TypedResults.Ok(MapStatus(projection));
                })
            .WithName("GetProjectionStatus")
            .Produces<ProjectionStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet(
                "/elections/{electionId}/canonical-state",
                async Task<Results<Ok<CanonicalElectionState>, NotFound>>(
                    string electionId,
                    ISolanaSettlementReadAdapter solanaSettlementReadAdapter,
                    CancellationToken cancellationToken) =>
                {
                    var state = await solanaSettlementReadAdapter.GetElectionStateAsync(electionId, cancellationToken).ConfigureAwait(false);
                    return state is null ? TypedResults.NotFound() : TypedResults.Ok(state);
                })
            .WithName("GetCanonicalElectionState")
            .Produces<CanonicalElectionState>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost(
                "/elections/{electionId}/ingest",
                async Task<Ok<ProjectionStatusResponse>>(
                    string electionId,
                    ProjectionIngestionRequest request,
                    IProjectionManagementService projectionManagementService,
                    CancellationToken cancellationToken) =>
                {
                    var evidence = new ProjectionEvidenceIngestion(
                        electionId,
                        request.LifecycleState,
                        request.ResultDigest,
                        request.SettlementReference,
                        request.CandidateTallies,
                        request.VidEntries.Select(v => new VidEvidenceEntry(
                            v.Vid,
                            v.BatchId,
                            v.BallotCommitment,
                            v.InclusionProofReference,
                            v.RecordedAtUtc,
                            v.ConsistencyDigest)).ToArray(),
                        request.MetadataDigest,
                        request.AuditDigest,
                        request.IsStale,
                        request.StaleReason);

                    var projection = await projectionManagementService.IngestEvidenceAsync(evidence, cancellationToken).ConfigureAwait(false);
                    return TypedResults.Ok(MapStatus(projection));
                })
            .WithName("IngestProjectionEvidence")
            .Produces<ProjectionStatusResponse>(StatusCodes.Status200OK)
            .RequireAuthorization(policy => policy.RequireRole(TransparencyRoles.Ingestion));

        group.MapPost(
                "/elections/{electionId}/mark-stale",
                async Task<Results<Ok<ProjectionStatusResponse>, NotFound>>(
                    string electionId,
                    StaleProjectionRequest request,
                    IProjectionManagementService projectionManagementService,
                    CancellationToken cancellationToken) =>
                {
                    var projection = await projectionManagementService.MarkStaleAsync(electionId, request.Reason, cancellationToken).ConfigureAwait(false);
                    return projection is null ? TypedResults.NotFound() : TypedResults.Ok(MapStatus(projection));
                })
            .WithName("MarkProjectionStale")
            .Produces<ProjectionStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(policy => policy.RequireRole(TransparencyRoles.Ingestion));

        group.MapPost(
                "/elections/{electionId}/reindex",
                async Task<Results<Ok<ProjectionStatusResponse>, NotFound>>(
                    string electionId,
                    IProjectionManagementService projectionManagementService,
                    CancellationToken cancellationToken) =>
                {
                    var projection = await projectionManagementService.ReindexElectionAsync(electionId, cancellationToken).ConfigureAwait(false);
                    return projection is null ? TypedResults.NotFound() : TypedResults.Ok(MapStatus(projection));
                })
            .WithName("ReindexProjection")
            .Produces<ProjectionStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(policy => policy.RequireRole(TransparencyRoles.Ingestion));

        group.MapPost(
                "/events/resync",
                async Task<IResult>(
                    CanonicalResyncTriggerRequest request,
                    ICanonicalResyncEventSource eventSource,
                    CancellationToken cancellationToken) =>
                {
                    await eventSource.PublishAsync(
                            new CanonicalResyncEvent(
                                request.ElectionId,
                                request.Reason,
                                request.Source,
                                request.CorrelationId,
                                DateTimeOffset.UtcNow),
                            cancellationToken)
                        .ConfigureAwait(false);

                    return TypedResults.Ok(new { queued = true, request.ElectionId, request.Source, request.CorrelationId });
                })
            .WithName("QueueCanonicalResyncEvent")
            .Produces(StatusCodes.Status200OK)
            .RequireAuthorization(policy => policy.RequireRole(TransparencyRoles.Ingestion));

        group.MapPost(
                "/reconciliation/disputes",
                async Task<ReconciliationCaseResponse>(
                    ReconciliationDisputeRequest request,
                    IReconciliationService reconciliationService,
                    CancellationToken cancellationToken) =>
                {
                    var created = await reconciliationService.OpenDisputeAsync(
                            request.ElectionId,
                            request.Actor,
                            request.Reason,
                            request.Notes,
                            request.ExternalEvidenceDigest,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return MapReconciliationCase(created);
                })
            .WithName("OpenReconciliationDispute")
            .Produces<ReconciliationCaseResponse>(StatusCodes.Status200OK)
            .RequireAuthorization(policy => policy.RequireRole(TransparencyRoles.Reconciliation));

        group.MapPost(
                "/reconciliation/{caseId}/override",
                async Task<Results<Ok<ReconciliationCaseResponse>, NotFound>>(
                    string caseId,
                    ReconciliationOverrideRequest request,
                    IReconciliationService reconciliationService,
                    CancellationToken cancellationToken) =>
                {
                    var updated = await reconciliationService.RequestOverrideAsync(
                            caseId,
                            request.Actor,
                            request.Reason,
                            request.Resolution,
                            request.Notes,
                            request.ExternalEvidenceDigest,
                            cancellationToken)
                        .ConfigureAwait(false);

                    return updated is null
                        ? TypedResults.NotFound()
                        : TypedResults.Ok(MapReconciliationCase(updated));
                })
            .WithName("RequestReconciliationOverride")
            .Produces<ReconciliationCaseResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(policy => policy.RequireRole(TransparencyRoles.Reconciliation));

        group.MapPost(
                "/reconciliation/{caseId}/resolve",
                async Task<Results<Ok<ReconciliationCaseResponse>, NotFound>>(
                    string caseId,
                    ReconciliationResolveRequest request,
                    IReconciliationService reconciliationService,
                    CancellationToken cancellationToken) =>
                {
                    var updated = await reconciliationService.ResolveAsync(
                            caseId,
                            request.Actor,
                            request.Reason,
                            request.Outcome,
                            request.Notes,
                            cancellationToken)
                        .ConfigureAwait(false);

                    return updated is null
                        ? TypedResults.NotFound()
                        : TypedResults.Ok(MapReconciliationCase(updated));
                })
            .WithName("ResolveReconciliationCase")
            .Produces<ReconciliationCaseResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(policy => policy.RequireRole(TransparencyRoles.Reconciliation));

        group.MapGet(
                "/elections/{electionId}/reconciliation/cases",
                async Task<IResult>(
                    string electionId,
                    IReconciliationService reconciliationService,
                    CancellationToken cancellationToken) =>
                {
                    var items = await reconciliationService.ListByElectionAsync(electionId, cancellationToken).ConfigureAwait(false);
                    IReadOnlyList<ReconciliationCaseResponse> mapped = items.Select(MapReconciliationCase).ToArray();
                    return TypedResults.Ok(mapped);
                })
            .WithName("ListReconciliationCases")
            .Produces<IReadOnlyList<ReconciliationCaseResponse>>(StatusCodes.Status200OK);

        return app;
    }

    private static ProjectionStatusResponse MapStatus(ElectionProjection projection)
        => new(
            projection.ElectionId,
            projection.LifecycleState,
            projection.IsStale,
            projection.StaleReason,
            projection.ConsistencyDigest,
            projection.MetadataDigest,
            projection.AuditDigest,
            projection.VidEntries.Count,
            projection.GeneratedAtUtc);

    private static ReconciliationCaseResponse MapReconciliationCase(ReconciliationCase source)
        => new(
            source.CaseId,
            source.ElectionId,
            source.Status,
            source.CurrentDigest,
            source.ExternalEvidenceDigest,
            source.OpenedBy,
            source.OpenReason,
            source.OpenedAtUtc,
            source.UpdatedAtUtc,
            source.Events
                .Select(e => new ReconciliationEventResponse(
                    e.EventType,
                    e.Actor,
                    e.Reason,
                    e.Notes,
                    e.ExternalEvidenceDigest,
                    e.OccurredAtUtc))
                .ToArray());
}
