using ChainBallot.PublicContracts;
using ElectionProcessing.Api.Application;
using ElectionProcessing.Api.Processing;

namespace ElectionProcessing.Api.Endpoints;

/// <summary>
/// Maps Election Processing Component ("Outbound") HTTP endpoints: ballot ingest, operational
/// status/state, prover readiness, and per-election pause/resume/retry-failed-batch controls
/// (Architecture.md § 3.6).
/// </summary>
public static class ElectionProcessingEndpoints
{
    /// <summary>Registers Election Processing endpoints.</summary>
    public static IEndpointRouteBuilder MapElectionProcessingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/processing/status", (IProcessingCoordinator coordinator) =>
            Results.Ok(coordinator.GetStatus()));

        app.MapGet("/api/processing/elections/{electionId}/state", (string electionId, IProcessingCoordinator coordinator) =>
            Results.Ok(coordinator.GetElectionState(electionId)));

        app.MapGet("/api/processing/prover/readiness", async (IProverReadinessService readinessService, CancellationToken cancellationToken) =>
        {
            var readiness = await readinessService.GetReadinessAsync(cancellationToken);
            return readiness.IsReady
                ? Results.Ok(readiness)
                : Results.Problem(title: "Prover not ready", detail: readiness.Reason, statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        app.MapPost("/api/processing/elections/{electionId}/pause", (string electionId, IProcessingCoordinator coordinator) =>
        {
            var changed = coordinator.PauseElection(electionId);
            return Results.Ok(new { electionId, isPaused = true, changed });
        }).RequireAuthorization(policy => policy.RequireRole(ProcessingRoles.Operator));

        app.MapPost("/api/processing/elections/{electionId}/resume", (string electionId, IProcessingCoordinator coordinator) =>
        {
            var changed = coordinator.ResumeElection(electionId);
            return Results.Ok(new { electionId, isPaused = false, changed });
        }).RequireAuthorization(policy => policy.RequireRole(ProcessingRoles.Operator));

        app.MapPost("/api/processing/elections/{electionId}/retry-failed-batch", async (string electionId, IProcessingCoordinator coordinator, CancellationToken cancellationToken) =>
        {
            var outcome = await coordinator.RetryFailedBatchAsync(electionId, cancellationToken);
            return outcome switch
            {
                BatchRetryOutcome.Retried => Results.Ok(new { electionId, retried = true }),
                BatchRetryOutcome.ProofVerificationFailed => Results.Conflict(new { electionId, retried = false, message = "Proof re-verification failed; the batch remains blocked and was not settled." }),
                _ => Results.NotFound(new { electionId, retried = false, message = "No failed batch is available for retry." })
            };
        }).RequireAuthorization(policy => policy.RequireRole(ProcessingRoles.Operator));

        app.MapPost("/api/processing/ingest", async (BallotSubmissionPackage package, IProcessingCoordinator coordinator, CancellationToken cancellationToken) =>
        {
            var result = await coordinator.EnqueueAsync(package, cancellationToken);
            if (!result.Accepted)
            {
                return Results.BadRequest(new { package.ElectionId, package.SubmissionNonce, reason = result.RejectionReason });
            }

            return Results.Accepted(value: new
            {
                package.ElectionId,
                package.SubmissionNonce,
                queuedAtUtc = DateTimeOffset.UtcNow
            });
        }).RequireAuthorization(policy => policy.RequireRole(ProcessingRoles.Ingest));

        return app;
    }
}
